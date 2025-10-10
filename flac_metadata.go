package main

import (
	"fmt"
	"os"
)

const FlacMarker = 0x664C6143

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

	markerBuffer := make([]byte, 4)
	marker, err := file.Read(markerBuffer)
	check(err)
}
