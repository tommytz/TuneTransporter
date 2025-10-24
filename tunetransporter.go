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
	"strings"
)

const PathSeparatorReplacement = "+"
const MusicDirEnvVar = "TUNE_TRANSPORTER_MUSIC_PATH"
const SlskdEventEnvVar = "SLSKD_SCRIPT_DATA"

type SlskdEvent struct {
	Id                  string
	Timestamp           string
	Type                string
	Version             int
	LocalDirectoryName  string
	RemoteDirectoryName string
	Username            string
}

// TODO
// cli args to work with file, list of file, or directory
// logging to a file so I can debug later if things don't work
// cli arg for verbose logging (ie do I want to see it in stdout) of metadata parsing
// change music dir from env var to cli arg
// create modules and refactor
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
		header, err := ReadBlockHeader(file)
		if err != nil {
			return fmt.Errorf("Unable to parse block header: %w", err)
		}

		if header.BlockType == VorbisCommentType {
			metadata, _ := ParseVorbisComment(file)

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
