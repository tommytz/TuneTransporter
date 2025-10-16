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
const PathSeparatorReplacement = "+"
const MusicDirEnvVar = "MUSIC_DIR"

type BlockHeader struct {
	IsLast    bool
	BlockType byte
	BlockSize uint32
}

type Metadata struct {
	Title       string
	AlbumArtist string
	Album       string
	TrackNumber int
	DiscNumber  int
	DiscTotal   int
}

func main() {
	musicDir, found := os.LookupEnv(MusicDirEnvVar)
	if !found {
		log.Fatalf("Missing environment variable %s", MusicDirEnvVar)
	}

	fmt.Println(musicDir) // TODO: Remove this later

	if len(os.Args) == 1 {
		log.Fatal("Missing filename argument") // TODO: Remove this when using events from slskd
	}

	filename := os.Args[1] // TODO: Remove this when using events from slskd

	file, err := os.Open(filename)
	if err != nil {
		log.Fatalf("Unable to open file: %v", filename)
	}

	defer file.Close() // Find out if this is necessary

	var signature uint32

	err = binary.Read(file, binary.BigEndian, &signature)
	if err != nil {
		file.Close()
		log.Fatalf("Unable to read file signature: %v", err)
	}

	if signature != FlacSignature {
		file.Close()
		log.Fatalf("%v is not a .flac file.", filename)
	}

	for {
		header, err := readBlockHeader(file)
		if err != nil {
			file.Close()
			log.Fatalf("Unable to parse block header: %v", err)
		}

		if header.BlockType == VorbisCommentType {
			metadata, _ := parseVorbisComment(file)
			fmt.Printf("%+v\n", metadata)

			formattedFilename := formatFilename(metadata)
			fmt.Println(formattedFilename)
		} else {
			_, err = file.Seek(int64(header.BlockSize), io.SeekCurrent)
			if err != nil {
				file.Close()
				log.Fatalf("Failed to seek past metadata block: %v, %v", header.BlockType, err)
			}
		}

		if header.IsLast {
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

func parseVorbisComment(reader io.ReadSeeker) (*Metadata, error) {
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

	metadata, err := parseFields(numberOfFields, reader)
	if err != nil {
		return nil, err
	}

	return metadata, nil
}

func parseFields(numberOfFields uint32, reader io.Reader) (*Metadata, error) {
	metadata := Metadata{}

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

		switch fieldParts[0] {
		case "TITLE":
			metadata.Title = fieldParts[1]
		case "ALBUMARTIST":
			metadata.AlbumArtist = fieldParts[1]
		case "ALBUM":
			metadata.Album = fieldParts[1]
		case "TRACKNUMBER":
			i, err := strconv.Atoi(fieldParts[1])
			if err != nil {
				return nil, err
			}

			metadata.TrackNumber = i
		case "DISCNUMBER":
			i, err := strconv.Atoi(fieldParts[1])
			if err != nil {
				return nil, err
			}

			metadata.DiscNumber = i
		case "DISCTOTAL":
			i, err := strconv.Atoi(fieldParts[1])
			if err != nil {
				return nil, err
			}

			metadata.DiscTotal = i
		}

	}

	return &metadata, nil
}

func formatFilename(metadata *Metadata) string {
	var out string

	if metadata.DiscTotal > 1 {
		// {medium:0}{track:00} - {Track Title}
		out = fmt.Sprintf("%d%02d - %s.flac", metadata.DiscNumber, metadata.TrackNumber, metadata.Title)
	} else {
		// {track:00} - {Track Title}
		out = fmt.Sprintf("%02d - %s.flac", metadata.TrackNumber, metadata.Title)
	}

	sanitised := strings.ReplaceAll(out, string(os.PathSeparator), PathSeparatorReplacement)

	return sanitised
}
