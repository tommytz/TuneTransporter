package main

import (
	"encoding/binary"
	"errors"
	"fmt"
	"io"
	"os"
)

const FlacMarker uint32 = 0x664C6143

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

	var marker uint32
	err = binary.Read(reader, binary.BigEndian, &marker)
	check(err)

	if marker != FlacMarker {
		fmt.Println("This is not a flac file! Exiting...")
		panic(errors.New("Cannot read a non-flac file"))
	}

	fmt.Println("This is a flac file!")

	firstBlock := readBlockHeader(reader)

	fmt.Println(firstBlock.isLast)
	fmt.Println(firstBlock.blockType)
	fmt.Println(firstBlock.blockSize)
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
