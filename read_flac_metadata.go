package main

import (
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

func check(e error) {
	if e != nil {
		panic(e)
	}
}

type blockHeader struct {
	isLast    bool
	blockType byte
	blockSize uint32
}

func main() {
	filename := os.Args[1]

	fmt.Println(filename)

	file, err := os.Open(filename)
	check(err)

	defer file.Close()

	reader := io.Reader(file)

	fmt.Println("Reading file signature")

	var signature uint32
	err = binary.Read(reader, binary.BigEndian, &signature)
	check(err)

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
		default:
			fmt.Printf("Other metadata block of type %v\n", header.blockType)
		}

		if header.isLast {
			break
		}

		_, err := file.Seek(int64(header.blockSize), 1)
		check(err)
	}
}

func readBlockHeader(reader io.Reader) *blockHeader {
	raw := make([]byte, 4)

	_, err := reader.Read(raw)
	check(err)

	header := blockHeader{
		isLast:    (raw[0] >> 7) == 1,
		blockType: raw[0] & 0x7f,
		blockSize: uint32(raw[1])<<16 | uint32(raw[2])<<8 | uint32(raw[3]),
	}

	return &header
}
