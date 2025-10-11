package main

import (
	"bytes"
	"encoding/binary"
	"errors"
	"fmt"
	"io"
	"os"
)

const FlacSignature uint32 = 0x664C6143

const (
	StreamInfoType    = 0
	VorbisCommentType = 4
)

type blockHeader struct {
	isLast    bool
	blockType byte
	blockSize uint32
}

func main() {
	filename := os.Args[1]

	fmt.Println(filename)

	file, err := os.Open(filename)
	if err != nil {
		panic(err)
	}

	defer file.Close()

	reader := io.Reader(file)

	fmt.Println("Reading file signature")
	var signature uint32
	binary.Read(reader, binary.BigEndian, &signature)

	if signature != FlacSignature {
		fmt.Println("This is not a flac file! Exiting...")
		panic(errors.New("Cannot read a non-flac file"))
	}

	fmt.Println("This is a flac file!")

	for {
		header := readBlockHeader(reader)

		switch header.blockType {
		case StreamInfoType:
			fmt.Printf("Streaminfo metadata block, %v bytes in length\n", header.blockSize)
		case VorbisCommentType:
			fmt.Printf("Vorbis comment metadata block, %v bytes in length\n", header.blockSize)

			buffer := make([]byte, header.blockSize)
			io.ReadFull(reader, buffer)
			bytesReader := bytes.NewReader(buffer)

			var vendorLength uint32
			binary.Read(bytesReader, binary.LittleEndian, &vendorLength)
			fmt.Printf("Vendor string length: %v bytes\n", vendorLength)

			vendor := make([]byte, vendorLength)
			io.ReadFull(bytesReader, vendor)
			vendorString := string(vendor[:])
			fmt.Println(vendorString)

			var numberOfFields uint32
			binary.Read(bytesReader, binary.LittleEndian, &numberOfFields)
			fmt.Printf("Number of fields in vorbis comment: %v \n", numberOfFields)

			for range numberOfFields {
				var fieldLength uint32
				binary.Read(bytesReader, binary.LittleEndian, &fieldLength)

				field := make([]byte, fieldLength)
				io.ReadFull(bytesReader, field)
				fieldString := string(field[:])
				fmt.Println(fieldString)
			}
		default:
			fmt.Printf("Other metadata block of type %v\n", header.blockType)
		}

		if header.isLast {
			break
		}

		if header.blockType != VorbisCommentType {
			file.Seek(int64(header.blockSize), 1)
		}
	}
}

func readBlockHeader(reader io.Reader) *blockHeader {
	raw := make([]byte, 4)

	reader.Read(raw)

	header := blockHeader{
		isLast:    (raw[0] >> 7) == 1,
		blockType: raw[0] & 0x7f,
		blockSize: uint32(raw[1])<<16 | uint32(raw[2])<<8 | uint32(raw[3]),
	}

	return &header
}
