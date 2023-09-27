meta:
  id: majiro_readmark
  file-extension: mss
  encoding: shift-jis
  endian: le

seq:
  - id: entries
    type: entry
    repeat: eos
  
types:
  entry:
    seq:
      - id: script_name
        type: str
        size: 128
      - id: size
        type: u4
      - id: text_count
        type: u4
      - id: placeholder_data
        type: u4
      - id: placeholder_prev
        type: u4
      - id: placeholder_next
        type: u4
      - id: data
        size: size