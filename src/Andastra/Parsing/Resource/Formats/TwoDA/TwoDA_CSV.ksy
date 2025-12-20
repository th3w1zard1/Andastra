meta:
  id: twoda_csv
  title: BioWare TwoDA CSV Format
  license: MIT
  endian: le
  file-extension: 2da.csv
  encoding: UTF-8
doc: |
  TwoDA CSV format is a human-readable CSV (Comma-Separated Values) representation of TwoDA files.
  Provides easier editing in spreadsheet applications than binary TwoDA format.
  
  Each row represents a data row, with the first row containing column headers.

seq:
  - id: csv_content
    type: str
    size-eos: true
    encoding: UTF-8
    doc: CSV text content with rows separated by newlines and columns by commas

