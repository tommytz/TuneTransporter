package main

import (
	"encoding/binary"
	"fmt"
	"io"
	"log"
	"os"
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

type Metadata struct {
	Title       string
	Artist      string
	AlbumArtist string
	Album       string
	TrackNumber int
	DiscNumber  int
	DiscTotal   int
}

func main() {
	filename := os.Args[1]
	// TODO: Find how to get the full path to the file

	fmt.Println(filename)

	file, err := os.Open(filename)
	if err != nil {
		log.Fatalf("Unable to open file: %v", filename)
	}

	defer file.Close() // Find out if this is necessary

	reader := io.Reader(file)

	var signature uint32

	err = binary.Read(reader, binary.BigEndian, &signature)
	if err != nil {
		file.Close()
		log.Fatalf("Unable to read file signature: %v", err)
	}

	if signature != FlacSignature {
		file.Close()
		log.Fatalf("%v is not a .flac file.", filename)
	}

	for {
		header, err := readBlockHeader(reader)
		if err != nil {
			file.Close()
			log.Fatalf("Unable to parse block header: %v", err)
		}

		if header.BlockType == VorbisCommentType {
			metadata, _ := parseVorbisComment(reader)
			fmt.Printf("%+v\n", metadata)
		} else {
			_, err = file.Seek(int64(header.BlockSize), 1)
			if err != nil {
				file.Close()
				log.Fatalf("Failed to seek past metadata block: %v, %v", header.BlockType, err)
			}
		}

		if header.IsLast {
			fmt.Println("No more metadata blocks to parse")
			file.Close()
			break
		}
	}
}

func readBlockHeader(reader io.Reader) (*BlockHeader, error) {
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

func parseVorbisComment(reader io.Reader) (*Metadata, error) {
	var vendorLength uint32
	binary.Read(reader, binary.LittleEndian, &vendorLength)
	fmt.Printf("Vendor string length: %v bytes\n", vendorLength)

	vendor := make([]byte, vendorLength)
	io.ReadFull(reader, vendor)
	vendorString := string(vendor[:])
	fmt.Println(vendorString)

	var numberOfFields uint32
	binary.Read(reader, binary.LittleEndian, &numberOfFields)
	fmt.Printf("Number of fields in vorbis comment: %v \n", numberOfFields)

	metadata := Metadata{}

	for range numberOfFields {
		var fieldLength uint32
		binary.Read(reader, binary.LittleEndian, &fieldLength)

		field := make([]byte, fieldLength)
		io.ReadFull(reader, field)
		fieldString := string(field[:])

		fieldParts := strings.Split(fieldString, "=")

		switch fieldParts[0] {
		case "TITLE":
			metadata.Title = fieldParts[1]
		case "ARTIST":
			metadata.Artist = fieldParts[1]
		case "ALBUMARTIST":
			metadata.AlbumArtist = fieldParts[1]
		case "ALBUM":
			metadata.Album = fieldParts[1]
		case "TRACKNUMBER":
			i, _ := strconv.Atoi(fieldParts[1])
			metadata.TrackNumber = i
		case "DISCNUMBER":
			i, _ := strconv.Atoi(fieldParts[1])
			metadata.DiscNumber = i
		case "DISCTOTAL":
			i, _ := strconv.Atoi(fieldParts[1])
			metadata.DiscTotal = i
		}
	}

	return &metadata, nil
}
