#!/usr/bin/env python3
"""
TPC Parser Comparison Script

This script parses a TPC file and outputs structured data for comparison
between Andastra parser and Kaitai parser implementations.

Usage: python compare_tpc_parsers.py <tpc_file_path>
Output: JSON to stdout with parsed TPC structure
"""

import sys
import json
import struct
from typing import Dict, List, Any, Optional, Tuple


class TPCParser:
    """Manual TPC parser matching Andastra implementation"""
    
    # Format mappings matching TPCTextureFormat enum
    FORMAT_INVALID = -1
    FORMAT_GREYSCALE = 0
    FORMAT_DXT1 = 1
    FORMAT_DXT3 = 2
    FORMAT_DXT5 = 3
    FORMAT_RGB = 4
    FORMAT_RGBA = 5
    FORMAT_BGRA = 6
    FORMAT_BGR = 7
    
    MAX_DIMENSIONS = 0x8000
    IMG_DATA_START_OFFSET = 0x80
    TOTAL_CUBE_SIDES = 6
    
    @staticmethod
    def get_format_size(format_type: int, width: int, height: int) -> int:
        """Calculate data size for a given format and dimensions"""
        if format_type in [TPCParser.FORMAT_DXT1, TPCParser.FORMAT_DXT3, TPCParser.FORMAT_DXT5]:
            bytes_per_block = 8 if format_type == TPCParser.FORMAT_DXT1 else 16
            return ((width + 3) // 4) * ((height + 3) // 4) * bytes_per_block
        elif format_type == TPCParser.FORMAT_GREYSCALE:
            return width * height * 1
        elif format_type in [TPCParser.FORMAT_RGB, TPCParser.FORMAT_BGR]:
            return width * height * 3
        elif format_type in [TPCParser.FORMAT_RGBA, TPCParser.FORMAT_BGRA]:
            return width * height * 4
        return 0
    
    @staticmethod
    def min_size(format_type: int) -> int:
        """Get minimum size for format"""
        if format_type == TPCParser.FORMAT_GREYSCALE:
            return 1
        elif format_type in [TPCParser.FORMAT_RGB, TPCParser.FORMAT_BGR]:
            return 3
        elif format_type in [TPCParser.FORMAT_RGBA, TPCParser.FORMAT_BGRA]:
            return 4
        elif format_type == TPCParser.FORMAT_DXT1:
            return 8
        elif format_type in [TPCParser.FORMAT_DXT3, TPCParser.FORMAT_DXT5]:
            return 16
        return 0
    
    @staticmethod
    def parse_tpc(filepath: str) -> Dict[str, Any]:
        """Parse TPC file and return structured data"""
        with open(filepath, 'rb') as f:
            data = f.read()
        
        if len(data) < TPCParser.IMG_DATA_START_OFFSET:
            raise ValueError(f"TPC file too small: {len(data)} bytes")
        
        # Read header (offset 0x00)
        data_size = struct.unpack('<I', data[0:4])[0]  # u4
        compressed = data_size != 0
        alpha_test = struct.unpack('<f', data[4:8])[0]  # f4
        width = struct.unpack('<H', data[8:10])[0]  # u2
        height = struct.unpack('<H', data[10:12])[0]  # u2
        pixel_type = data[12]  # u1
        mipmap_count = data[13]  # u1
        
        # Determine format
        format_type = TPCParser.FORMAT_INVALID
        if compressed:
            if pixel_type == 2:
                format_type = TPCParser.FORMAT_DXT1
            elif pixel_type == 4:
                format_type = TPCParser.FORMAT_DXT5
        else:
            if pixel_type == 1:
                format_type = TPCParser.FORMAT_GREYSCALE
            elif pixel_type == 2:
                format_type = TPCParser.FORMAT_RGB
            elif pixel_type == 4:
                format_type = TPCParser.FORMAT_RGBA
            elif pixel_type == 12:
                format_type = TPCParser.FORMAT_BGRA
        
        if format_type == TPCParser.FORMAT_INVALID:
            raise ValueError(f"Unsupported texture format (pixel_type: {pixel_type}, compressed: {compressed})")
        
        # Calculate layer count (check for cube map)
        layer_count = 1
        is_cube_map = False
        original_height = height
        
        if not compressed:
            data_size = TPCParser.get_format_size(format_type, width, height)
        elif height != 0 and width != 0 and (height // width) == TPCParser.TOTAL_CUBE_SIDES:
            is_cube_map = True
            height = height // TPCParser.TOTAL_CUBE_SIDES
            layer_count = TPCParser.TOTAL_CUBE_SIDES
        
        # Calculate complete data size including mipmaps
        complete_data_size = data_size
        for level in range(1, mipmap_count):
            reduced_width = max(width >> level, 1)
            reduced_height = max(height >> level, 1)
            complete_data_size += TPCParser.get_format_size(format_type, reduced_width, reduced_height)
        complete_data_size *= layer_count
        
        # Read TXI footer if present
        txi_start = TPCParser.IMG_DATA_START_OFFSET + 0x72 + complete_data_size
        txi_data = ""
        if txi_start < len(data):
            try:
                txi_data = data[txi_start:].decode('ascii', errors='ignore').strip()
            except:
                pass
        
        # Parse texture data
        layers = []
        pos = TPCParser.IMG_DATA_START_OFFSET
        
        for layer_idx in range(layer_count):
            layer = {
                "mipmaps": []
            }
            layer_width = width
            layer_height = height
            layer_size = TPCParser.get_format_size(format_type, layer_width, layer_height) if not is_cube_map else data_size
            
            for mip_idx in range(mipmap_count):
                mm_width = max(1, layer_width)
                mm_height = max(1, layer_height)
                mm_size = max(layer_size, TPCParser.min_size(format_type))
                
                if pos + mm_size > len(data):
                    break
                
                mm_data = data[pos:pos + mm_size]
                layer["mipmaps"].append({
                    "width": mm_width,
                    "height": mm_height,
                    "format": format_type,
                    "data_size": len(mm_data)
                })
                
                pos += mm_size
                
                if pos >= len(data):
                    break
                
                layer_width >>= 1
                layer_height >>= 1
                layer_size = TPCParser.get_format_size(format_type, max(1, layer_width), max(1, layer_height))
                
                if layer_width < 1 and layer_height < 1:
                    break
            
            layers.append(layer)
        
        # Build result structure
        result = {
            "alpha_test": alpha_test,
            "width": width,
            "height": original_height,  # Use original height before cube map adjustment
            "format": format_type,
            "format_name": TPCParser.format_name(format_type),
            "is_compressed": compressed,
            "is_cube_map": is_cube_map,
            "mipmap_count": mipmap_count,
            "layer_count": len(layers),
            "txi_present": len(txi_data) > 0,
            "txi_length": len(txi_data),
            "layers": layers
        }
        
        return result
    
    @staticmethod
    def format_name(format_type: int) -> str:
        """Get format name string"""
        names = {
            TPCParser.FORMAT_INVALID: "Invalid",
            TPCParser.FORMAT_GREYSCALE: "Greyscale",
            TPCParser.FORMAT_DXT1: "DXT1",
            TPCParser.FORMAT_DXT3: "DXT3",
            TPCParser.FORMAT_DXT5: "DXT5",
            TPCParser.FORMAT_RGB: "RGB",
            TPCParser.FORMAT_RGBA: "RGBA",
            TPCParser.FORMAT_BGRA: "BGRA",
            TPCParser.FORMAT_BGR: "BGR"
        }
        return names.get(format_type, f"Unknown({format_type})")


def main():
    if len(sys.argv) != 2:
        print("Usage: python compare_tpc_parsers.py <tpc_file_path>", file=sys.stderr)
        sys.exit(1)
    
    tpc_file = sys.argv[1]
    
    try:
        result = TPCParser.parse_tpc(tpc_file)
        print(json.dumps(result, indent=2))
    except Exception as e:
        print(json.dumps({"error": str(e)}, indent=2), file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()

