# Format layout
A FLAC bitstream consists of the fLaC (i.e., 0x664C6143) marker at the beginning of the stream,
followed by a mandatory metadata block (called the streaminfo metadata block),
any number of other metadata blocks, and then the audio frames.

e.g.

```
00000000  66 4c 61 43 00 00 00 22  10 00 10 00 00 00 10 00  |fLaC..."........|
```

# Metadata block header
Each metadata block starts with a 4-byte header.

The first bit in this header flags whether a metadata block is the last one.
It is 0 when other metadata blocks follow; otherwise, it is 1.
The 7 remaining bits of the first header byte contain the type of the metadata block as an unsigned unmber between 0 and 126.
The three bytes that follow code for the size of the size of the metadata block in bytes,
excluding the 4 header bytes, as an unsigned number coded big-endian.

    +=========+=======================================================+
    | Value   | Metadata Block Type                                   |
    +=========+=======================================================+
    | 0       | Streaminfo                                            |
    +---------+-------------------------------------------------------+
    | 1       | Padding                                               |
    +---------+-------------------------------------------------------+
    | 2       | Application                                           |
    +---------+-------------------------------------------------------+
    | 3       | Seek table                                            |
    +---------+-------------------------------------------------------+
    | 4       | Vorbis comment                                        |
    +---------+-------------------------------------------------------+
    | 5       | Cuesheet                                              |
    +---------+-------------------------------------------------------+
    | 6       | Picture                                               |
    +---------+-------------------------------------------------------+
    | 7 - 126 | Reserved                                              |
    +---------+-------------------------------------------------------+
    | 127     | Forbidden (to avoid confusion with a frame sync code) |
    +---------+-------------------------------------------------------+

The only metadata types we care about for tag metadata are the Streaminfo (since it comes first) and the Vorbis comment (where the tags are).

e.g.

For the first metadata header, 0x00000022, the first byte is 0x00, which is 0b00000000.

The first bit of this is 0, which tells us that there are more metadata blocks to follow.
The 7 remaining bits are 0b0000000, which is just 0 - which tells us this is a Streaminfo metadata block.

The remaining 3 bytes are 0x000022, which is 34 - so we know the rest of the metadata block is 34 bytes long.

After 34 bytes, the next metadata header is 0x030001e6. The first byte is 0x03.

The first bit is 0, so there are more metadata blocks to follow.
The remaining 7 bits are 0b0000011, which is 3 - so this is a Seek table.

The remaining 3 bytes are 0x0001e6, which is 486 - the size of the block...
We know that:
fLaC header + Streaminfo header + 34 + Seek table header
4 + 4 + 34 + 4 = 34 + 12 = 46
So our next metadata header will be 46 + 486 = 532.

00000210  66 df 10 00 04 00 03 dc  20 00 00 00 72 65 66 65  |f....... ...refe|

At 532 we have 0x040003dc, the first byte is 0x04, so we know there's more metadata, and that this is a Vorbis comment!
Furthermore, the remaining 3 bytes are 0x0003dc, so the Vorbis comment block is 988 bytes long!

# The Vorbis Comment
A Vorbis comment metadata block contains human-readable information coded in UTF-8 (which is just ASCII).

A Vorbis comment metadata block consists of a vendor string optionally followed by a number of fields,
which are pairs of field names and field contents (the metadata tags that we care about).

## Vendor string
The metadata block header is directly followed by 4 bytes containing the length in bytes of the vendor string
as an unsigned number coded little-endian. The vendor string follows, is UTF-8 coded and is not terminated in any way.

The 4 bytes are 0x20000000, which is 0x00000020 in little-endian, and 32 in decimal.

00000210  66 df 10 00 04 00 03 dc  20 00 00 00 72 65 66 65  |f....... ...refe|
00000220  72 65 6e 63 65 20 6c 69  62 46 4c 41 43 20 31 2e  |rence libFLAC 1.|
00000230  33 2e 30 20 32 30 31 33  30 35 32 36 1f 00 00 00  |3.0 20130526....|

The next 32 bytes give us 0x7265666572656e6365206c6962464c414320312e332e30203230313330353236
This translates in ASCII to the vendor string "reference libFLAC 1.3.0 20130526"!

## Fields
Immediately following the vendor string a 4 bytes containing the number of fields that are stored in the Vorbis comment block,
stored as an unsigned number coded little-endian. If this number is non-zero, it is followed by the fields themselves.

00000230  33 2e 30 20 32 30 31 33  30 35 32 36 1f 00 00 00  |3.0 20130526....|

Our 4 bytes are 0x1f000000, which is 0x0000001F in little-endian, and 31 in decimal. So there are 31 fields.

Each field is stored with a 4-byte length. The field length in bytes is stored as a 4-byte unsigned number coded little-endian.
The field itself follows it. Like the vendor string, the field is UTF-8 coded and not terminated in any way.
Each field consists of a field name and field contents, separated by an "=" character.

00000240  17 00 00 00 41 4c 42 55  4d 3d 41 6d 65 72 69 63  |....ALBUM=Americ|
00000250  61 6e 20 46 6f 6f 74 62  61 6c 6c 1d 00 00 00 41  |an Football....A|

The first 4 bytes are 0x17000000, which is 0x00000017 in little-endian, and 23 in decimal.
The next 23 bytes are:

0x414c42554d3d416d65726963616e20466f6f7462616c6c

This translates in ASCII to the field "ALBUM=American Football".

We can continue to do this for all the fields, and then keep reading metadata blocks until we get to one where the header says it's the last.
