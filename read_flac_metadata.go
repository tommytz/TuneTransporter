package main

import (
	"fmt"
	"os"
	"io"
	"encoding/binary"
)

const FlacMarker uint32 = 0x664C6143

func check(e error) {
	if e != nil {
		panic(e)
	}
}

func main () {
	filename := os.Args[1]

	fmt.Println(filename)

	file, err := os.Open(filename)
	check(err)

	defer file.Close()

	reader := io.Reader(file)

	var marker uint32
	err = binary.Read(reader, binary.BigEndian, &marker)
	check(err)

	if marker == FlacMarker {
		fmt.Println("This is a flac file!")
	} else {
		fmt.Println("This is not!")
	}
}
