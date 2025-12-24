#!/usr/bin/env python3
"""
Kaitai Parser Validation Script

This script validates the Kaitai-generated TPC parser by comparing its output
with the manual parser implementation. This provides actual parsing validation
as mentioned in the TODO comment.

Usage: python validate_kaitai_parser.py <tpc_file_path>
Output: JSON with validation results
"""

import sys
import os
import json
import importlib.util
from typing import Dict, Any, Optional

# Import the manual parser
from compare_tpc_parsers import TPCParser

def load_kaitai_parser():
    """Load the Kaitai-generated parser dynamically"""
    kaitai_path = os.path.join(os.path.dirname(__file__), 'test_kaitai_output', 'tpc.py')
    if not os.path.exists(kaitai_path):
        raise FileNotFoundError(f"Kaitai parser not found at {kaitai_path}")

    spec = importlib.util.spec_from_file_location("tpc_kaitai", kaitai_path)
    if spec is None or spec.loader is None:
        raise ImportError("Could not load Kaitai parser module")

    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

def parse_with_kaitai(module, filepath: str) -> Dict[str, Any]:
    """Parse TPC file using Kaitai-generated parser"""
    with open(filepath, 'rb') as f:
        data = f.read()

    # Create Kaitai stream and parse
    from kaitaistruct import KaitaiStream
    stream = KaitaiStream(data)
    tpc = module.Tpc(stream)

    # Extract basic information that Kaitai parser provides
    result = {
        "data_size": tpc.data_size,
        "alpha_test": tpc.alpha_test,
        "width": tpc.width,
        "height": tpc.height,
        "pixel_type": tpc.pixel_type,
        "mipmap_count": tpc.mipmap_count,
        "reserved": tpc.reserved,
        "texture_data_size": len(tpc.texture_data.raw_data),
        "kaitai_parsed": True
    }

    return result

def validate_parsers(tpc_file: str) -> Dict[str, Any]:
    """Validate Kaitai parser against manual parser"""
    try:
        # Load Kaitai parser
        kaitai_module = load_kaitai_parser()

        # Parse with both parsers
        manual_result = TPCParser.parse_tpc(tpc_file)
        kaitai_result = parse_with_kaitai(kaitai_module, tpc_file)

        # Compare results
        comparison = {
            "file": tpc_file,
            "manual_parser": manual_result,
            "kaitai_parser": kaitai_result,
            "validation": {},
            "overall_success": True,
            "errors": []
        }

        # Validate header fields that both parsers should agree on
        header_fields = ["data_size", "alpha_test", "width", "height", "pixel_type", "mipmap_count"]

        for field in header_fields:
            if field in manual_result and field in kaitai_result:
                manual_val = manual_result[field]
                kaitai_val = kaitai_result[field]

                if field == "alpha_test":
                    # Floating point comparison with tolerance
                    diff = abs(manual_val - kaitai_val)
                    matches = diff < 0.001
                else:
                    matches = manual_val == kaitai_val

                comparison["validation"][field] = {
                    "manual": manual_val,
                    "kaitai": kaitai_val,
                    "matches": matches
                }

                if not matches:
                    comparison["overall_success"] = False
                    comparison["errors"].append(f"{field} mismatch: manual={manual_val}, kaitai={kaitai_val}")
            else:
                comparison["validation"][field] = {
                    "error": f"Field {field} missing in one parser"
                }
                comparison["overall_success"] = False
                comparison["errors"].append(f"Field {field} missing in one parser")

        # Check if texture data size is reasonable
        if "texture_data_size" in kaitai_result:
            expected_min_size = 64  # Minimum reasonable texture data
            if kaitai_result["texture_data_size"] < expected_min_size:
                comparison["errors"].append(f"Texture data too small: {kaitai_result['texture_data_size']} bytes")
                comparison["overall_success"] = False

        return comparison

    except Exception as e:
        return {
            "file": tpc_file,
            "error": str(e),
            "overall_success": False
        }

def main():
    if len(sys.argv) != 2:
        print("Usage: python validate_kaitai_parser.py <tpc_file_path>", file=sys.stderr)
        sys.exit(1)

    tpc_file = sys.argv[1]

    if not os.path.exists(tpc_file):
        print(json.dumps({"error": f"TPC file not found: {tpc_file}"}, indent=2), file=sys.stderr)
        sys.exit(1)

    try:
        result = validate_parsers(tpc_file)
        print(json.dumps(result, indent=2))

        # Exit with error code if validation failed
        if not result.get("overall_success", False):
            sys.exit(1)

    except Exception as e:
        print(json.dumps({"error": str(e)}, indent=2), file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
