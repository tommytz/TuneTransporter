package main

import (
	"encoding/binary"
	"fmt"
	"io"
	"strconv"
	"strings"
)

const FlacSignature uint32 = 0x664C6143
const VorbisCommentType = 4

type BlockHeader struct {
	IsLast    bool
	BlockType byte
	BlockSize uint32
}

func ReadBlockHeader(reader io.Reader) (*BlockHeader, error) {
	raw := make([]byte, 4)

	_, err := reader.Read(raw)
	if err != nil {
		return nil, err
	}

	header := BlockHeader{
		IsLast:    (raw[0] >> 7) == 1,
		BlockType: raw[0] & 0x7f,
		BlockSize: uint32(raw[1])<<16 | uint32(raw[2])<<8 | uint32(raw[3]),
	}

	return &header, nil
}

func ParseVorbisComment(reader io.ReadSeeker) (*Metadata, error) {
	var vendorLength uint32
	err := binary.Read(reader, binary.LittleEndian, &vendorLength)
	if err != nil {
		return nil, err
	}

	_, err = reader.Seek(int64(vendorLength), io.SeekCurrent)
	if err != nil {
		return nil, err
	}

	var numberOfFields uint32
	err = binary.Read(reader, binary.LittleEndian, &numberOfFields)
	if err != nil {
		return nil, err
	}

	fields, err := readFields(numberOfFields, reader)
	if err != nil {
		return nil, err
	}

	for key, value := range fields {
		fmt.Printf("%v=%v\n", key, value)
	}

	trackNumber := safeAtoi(fields["TRACKNUMBER"])
	discNumber := safeAtoi(fields["DISCNUMBER"])
	discTotal := safeAtoi(fields["DISCTOTAL"])

	metadata := Metadata{
		Title:       fields["TITLE"],
		AlbumArtist: fields["ALBUMARTIST"],
		Album:       fields["ALBUM"],
		TrackNumber: trackNumber,
		DiscNumber:  discNumber,
		DiscTotal:   discTotal,
	}
	fmt.Printf("%+v\n", metadata)

	return &metadata, nil
}

func readFields(numberOfFields uint32, reader io.Reader) (map[string]string, error) {
	fields := make(map[string]string)

	for range numberOfFields {
		var fieldLength uint32
		err := binary.Read(reader, binary.LittleEndian, &fieldLength)
		if err != nil {
			return nil, err
		}

		field := make([]byte, fieldLength)
		_, err = io.ReadFull(reader, field)
		if err != nil {
			return nil, err
		}

		fieldString := string(field[:])
		fieldParts := strings.Split(fieldString, "=")
		fields[fieldParts[0]] = fieldParts[1]
	}

	return fields, nil
}

func safeAtoi(s string) int {
	if s == "" {
		return 0
	}

	n, err := strconv.Atoi(s)
	if err != nil {
		return 0
	}

	return n
}
