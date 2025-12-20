meta:
  id: wav
  title: BioWare WAV (Waveform Audio) Format
  license: MIT
  endian: le
  file-extension: wav
  xref:
    pykotor: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/wav/
    reone: vendor/reone/src/libs/audio/format/wavreader.cpp
    xoreos: vendor/xoreos/src/sound/decoders/wave.cpp
    wiki: vendor/PyKotor/wiki/WAV-File-Format.md
doc: |
  WAV (Waveform Audio Format) files used in KotOR. KotOR stores both standard WAV voice-over lines
  and Bioware-obfuscated sound-effect files. Voice-over assets are regular RIFF containers with PCM
  headers, while SFX assets prepend a 470-byte custom block before the RIFF data.
  
  Format Types:
  - VO (Voice-over): Plain RIFF/WAVE PCM files readable by any media player
  - SFX (Sound effects): Contains a Bioware 470-byte obfuscation header followed by RIFF data
  - MP3-in-WAV: Special RIFF container with MP3 data (RIFF size = 50)
  
  References:
  - vendor/PyKotor/wiki/WAV-File-Format.md
  - vendor/reone/src/libs/audio/format/wavreader.cpp:30-56
  - vendor/xoreos/src/sound/decoders/wave.cpp:34-84

seq:
  - id: header_detection
    type: header_detection
    doc: Detect header type and read appropriate header
  
  - id: riff_wave
    type: riff_wave
    doc: RIFF/WAVE structure (present in all WAV files, after optional headers)

types:
  header_detection:
    seq:
      - id: first_four_bytes
        type: u4
        doc: First 4 bytes to detect header type
    instances:
      is_sfx:
        value: (first_four_bytes & 0xFFFFFFFF) == 0xC460F3FF
        doc: SFX header detected (0xFF 0xF3 0x60 0xC4 in little-endian)
      is_vo:
        value: (first_four_bytes & 0xFFFFFFFF) == 0x46464952
        doc: VO header detected ("RIFF" = 0x46464952 in little-endian)
      is_standard:
        value: !is_sfx && (first_four_bytes & 0xFFFFFFFF) == 0x46464952
        doc: Standard RIFF/WAVE (no obfuscation header)
  
  sfx_header:
    seq:
      - id: magic_byte1
        type: u1
        doc: Magic byte 0xFF (already read as first_byte)
      
      - id: magic_byte2
        type: u1
        doc: Magic byte 0xF3
        valid: 0xF3
      
      - id: magic_byte3
        type: u1
        doc: Magic byte 0x60
        valid: 0x60
      
      - id: magic_byte4
        type: u1
        doc: Magic byte 0xC4
        valid: 0xC4
      
      - id: padding
        type: str
        size: 466
        doc: |
          Remaining 466 bytes of padding (typically zeros or 0x55 filler)
          Total SFX header size is 470 bytes (0x1DA)
          Reference: vendor/PyKotor/wiki/WAV-File-Format.md
  
  vo_header:
    seq:
      - id: magic_riff
        type: str
        encoding: ASCII
        size: 4
        doc: |
          VO header magic: "RIFF" (first byte already read as first_byte)
          Reference: vendor/PyKotor/src/pykotor/resource/formats/wav/wav_obfuscation.py:42-52
        valid: "RIFF"
      
      - id: padding
        type: str
        size: 16
        doc: |
          Remaining 16 bytes of padding (typically zeros)
          Total VO header size is 20 bytes
          Reference: vendor/PyKotor/src/pykotor/resource/formats/wav/wav_obfuscation.py:127-134
  
  riff_wave:
    seq:
      - id: riff_header
        type: riff_header
        doc: RIFF container header
      
      - id: chunks
        type: chunk
        repeat: until
        repeat-until: _io.eof
        doc: |
          RIFF chunks in sequence (fmt, fact, data, etc.)
          Parsed until end of file
          Reference: vendor/xoreos/src/sound/decoders/wave.cpp:46-55
    
    instances:
      is_mp3_in_wav:
        value: riff_header.riff_size == 50
        doc: |
          MP3-in-WAV format detected when RIFF size = 50
          Reference: vendor/PyKotor/src/pykotor/resource/formats/wav/wav_obfuscation.py:60-64
  
  riff_header:
    seq:
      - id: riff_id
        type: str
        encoding: ASCII
        size: 4
        doc: RIFF chunk ID: "RIFF"
        valid: "RIFF"
      
      - id: riff_size
        type: u4
        doc: |
          File size minus 8 bytes (RIFF_ID + RIFF_SIZE itself)
          For MP3-in-WAV format, this is 50
          Reference: vendor/PyKotor/wiki/WAV-File-Format.md
      
      - id: wave_id
        type: str
        encoding: ASCII
        size: 4
        doc: Format tag: "WAVE"
        valid: "WAVE"
  
  chunk:
    seq:
      - id: id
        type: str
        encoding: ASCII
        size: 4
        doc: |
          Chunk ID (4-character ASCII string)
          Common values: "fmt ", "data", "fact", "LIST", etc.
          Reference: vendor/xoreos/src/sound/decoders/wave.cpp:58-72
      
      - id: size
        type: u4
        doc: |
          Chunk size in bytes (chunk data only, excluding ID and size fields)
          Chunks are word-aligned (even byte boundaries)
          Reference: vendor/xoreos/src/sound/decoders/wave.cpp:66
      
      - id: body
        type: chunk_body
        doc: Chunk body (content depends on chunk ID)
  
  chunk_body:
    switch-on: _parent.id
    cases:
      '"fmt "': format_chunk_body
      '"data"': data_chunk_body
      '"fact"': fact_chunk_body
      _: unknown_chunk_body
    doc: Chunk body type depends on chunk ID
  
  format_chunk_body:
    seq:
      - id: audio_format
        type: u2
        doc: |
          Audio format code:
          - 0x0001 = PCM (Linear PCM, uncompressed)
          - 0x0002 = Microsoft ADPCM
          - 0x0006 = A-Law companded
          - 0x0007 = μ-Law companded
          - 0x0011 = IMA ADPCM (DVI ADPCM)
          - 0x0055 = MPEG Layer 3 (MP3)
          Reference: vendor/PyKotor/wiki/WAV-File-Format.md
      
      - id: channels
        type: u2
        doc: |
          Number of audio channels:
          - 1 = mono
          - 2 = stereo
          Reference: vendor/PyKotor/wiki/WAV-File-Format.md
      
      - id: sample_rate
        type: u4
        doc: |
          Sample rate in Hz
          Typical values:
          - 22050 Hz for SFX
          - 44100 Hz for VO
          Reference: vendor/PyKotor/wiki/WAV-File-Format.md
      
      - id: bytes_per_sec
        type: u4
        doc: |
          Byte rate (average bytes per second)
          Formula: sample_rate × block_align
          Reference: vendor/PyKotor/wiki/WAV-File-Format.md
      
      - id: block_align
        type: u2
        doc: |
          Block alignment (bytes per sample frame)
          Formula for PCM: channels × (bits_per_sample / 8)
          Reference: vendor/PyKotor/wiki/WAV-File-Format.md
      
      - id: bits_per_sample
        type: u2
        doc: |
          Bits per sample
          Common values: 8, 16
          For PCM: typically 16-bit
          Reference: vendor/PyKotor/wiki/WAV-File-Format.md
      
      - id: extra_format_bytes
        type: str
        size: _parent.size - 16
        if: _parent.size > 16
        doc: |
          Extra format bytes (present when fmt chunk size > 16)
          For IMA ADPCM and other compressed formats, contains:
          - Extra format size (u2)
          - Format-specific data (e.g., ADPCM coefficients)
          Reference: vendor/xoreos/src/sound/decoders/wave.cpp:66
    
    instances:
      is_pcm:
        value: audio_format == 1
        doc: True if audio format is PCM (uncompressed)
      
      is_ima_adpcm:
        value: audio_format == 0x11
        doc: True if audio format is IMA ADPCM (compressed)
      
      is_mp3:
        value: audio_format == 0x55
        doc: True if audio format is MP3
  
  data_chunk_body:
    seq:
      - id: data
        type: str
        size: _parent.size
        doc: |
          Raw audio data (PCM samples or compressed audio)
          Reference: vendor/xoreos/src/sound/decoders/wave.cpp:79-80
  
  fact_chunk_body:
    seq:
      - id: sample_count
        type: u4
        doc: |
          Sample count (number of samples in compressed audio)
          Used for compressed formats like ADPCM
          Reference: vendor/PyKotor/src/pykotor/resource/formats/wav/io_wav.py:189-192
  
  unknown_chunk_body:
    seq:
      - id: data
        type: str
        size: _parent.size
        doc: |
          Unknown chunk body (skip for compatibility)
          Reference: vendor/xoreos/src/sound/decoders/wave.cpp:53-54
      
      - id: padding
        type: u1
        if: _parent.size % 2 == 1
        doc: |
          Padding byte to align to word boundary (only if chunk size is odd)
          RIFF chunks must be aligned to 2-byte boundaries
          Reference: vendor/PyKotor/src/pykotor/resource/formats/wav/io_wav.py:153-156
