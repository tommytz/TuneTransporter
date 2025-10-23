package main

import (
	"encoding/binary"
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"log"
	"os"
	"path/filepath"
	"strconv"
	"strings"
)

const FlacSignature uint32 = 0x664C6143
const VorbisCommentType = 4
const PathSeparatorReplacement = "+"
const MusicDirEnvVar = "TUNE_TRANSPORTER_MUSIC_PATH"
const SlskdEventEnvVar = "SLSKD_SCRIPT_DATA"

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

type SlskdEvent struct {
	Id                  string
	Timestamp           string
	Type                string
	Version             int
	LocalDirectoryName  string
	RemoteDirectoryName string
	Username            string
}

func main() {
	musicDir, ok := os.LookupEnv(MusicDirEnvVar)
	if !ok {
		log.Fatalf("Missing environment variable: %s", MusicDirEnvVar)
	}

	filename := flag.String("file", "", "the name of the file to use")
	flag.Parse()

	fmt.Println(os.Args)
	fmt.Println(*filename)

	if *filename != "" {
		log.Println("Single file mode: ", *filename)

		err := processFile(*filename, musicDir)
		if err != nil {
			log.Fatalf("Couldn't parse file: %v", err)
		}

		os.Exit(0)
	}

	log.Println("JSON event parsing mode")
	jsonEvent, ok := os.LookupEnv(SlskdEventEnvVar)
	if !ok {
		log.Fatalf("Missing environment variable: %s", SlskdEventEnvVar)
	}

	var event SlskdEvent
	err := json.Unmarshal([]byte(jsonEvent), &event)
	if err != nil {
		log.Fatalf("Couldn't read event, %v", err)
	}

	files, err := readDirectory(event)
	if err != nil {
		log.Fatalf("Couldn't read directory contents, %v", err)
	}

	fmt.Printf("%v\n", files)

	// TODO: Move flac specific code into a flac submodule

	for _, filename := range files {
		// TODO: Create a context struct with state like env var directories and methods like processFile
		err := processFile(filename, musicDir)

		if err != nil {
			log.Printf("Skipping %v due to error: %v", filename, err)
			continue
		}
	}

	// TODO: Clean up directory afterwards if successful!
}

func readDirectory(event SlskdEvent) ([]string, error) {
	contents, err := os.ReadDir(event.LocalDirectoryName)
	if err != nil {
		return nil, err
	}

	var paths []string

	for _, file := range contents {
		base := file.Name()
		filename := filepath.Join(event.LocalDirectoryName, base)
		paths = append(paths, filename)
	}

	return paths, nil
}

func processFile(filename, musicDir string) error {
	file, err := os.Open(filename)
	if err != nil {
		return fmt.Errorf("Unable to open file: %w", err)
	}

	defer file.Close()

	var signature uint32
	err = binary.Read(file, binary.BigEndian, &signature)
	if err != nil {
		return fmt.Errorf("Unable to read file signature: %w", err)
	}

	if signature != FlacSignature {
		return fmt.Errorf("%v is not a .flac file.", filename)
	}

	for {
		header, err := readBlockHeader(file)
		if err != nil {
			return fmt.Errorf("Unable to parse block header: %w", err)
		}

		if header.BlockType == VorbisCommentType {
			metadata, _ := parseVorbisComment(file)
			fmt.Printf("%+v\n", metadata)

			newFilepath := formatFilepath(metadata, musicDir)
			fmt.Println(newFilepath)

			err = os.MkdirAll(filepath.Dir(newFilepath), 0777)
			if err != nil {
				return fmt.Errorf("Failed to create artist and album directory: %w", err)
			}

			err = os.Rename(filename, newFilepath)
			if err != nil {
				return fmt.Errorf("Failed to move file: %w", err)
			}
		} else {
			_, err = file.Seek(int64(header.BlockSize), io.SeekCurrent)
			if err != nil {
				return fmt.Errorf("Failed to seek past metadata block: %w", err)
			}
		}

		if header.IsLast {
			break
		}
	}

	return nil
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

func formatFilepath(metadata *Metadata, musicDir string) string {
	var filename string
	artist := sanitise(metadata.AlbumArtist)
	album := sanitise(metadata.Album)

	if metadata.DiscTotal > 1 {
		// {medium:0}{track:00} - {Track Title}
		filename = sanitise(fmt.Sprintf("%d%02d - %s.flac", metadata.DiscNumber, metadata.TrackNumber, metadata.Title))
	} else {
		// {track:00} - {Track Title}
		filename = sanitise(fmt.Sprintf("%02d - %s.flac", metadata.TrackNumber, metadata.Title))
	}

	return filepath.Join(musicDir, artist, album, filename)
}

func sanitise(s string) string {
	return strings.ReplaceAll(s, string(os.PathSeparator), PathSeparatorReplacement)
}
