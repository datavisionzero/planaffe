package cmd

import (
	"fmt"
	"io"
	"os"
	"strings"

	"github.com/spf13/cobra"

	"github.com/datavisionzero/planaffe/src/cli/internal/config"
)

// stdin is an invocation's standard input, which only one flag may read. A
// second `-` used to read what the first had left, which is nothing, and
// `issue edit --description-file - --result-file -` cleared the result with
// it.
type stdin struct {
	io.Reader
	taken bool
}

// readText reads a Markdown field from a file, or from stdin for `-` — never
// from an editor (VISION 6.1). An empty path means the flag was not given.
func readText(in io.Reader, path string) (*string, error) {
	if path == "" {
		return nil, nil
	}

	var data []byte
	var err error
	if path == "-" {
		if once, ok := in.(*stdin); ok {
			if once.taken {
				return nil, &config.UsageError{Message: "stdin can be read once: only one flag may be `-`; give the others a file."}
			}
			once.taken = true
		}
		data, err = io.ReadAll(in)
	} else {
		data, err = os.ReadFile(path)
	}
	if err != nil {
		return nil, &config.UsageError{Message: fmt.Sprintf("cannot read %s: %v", path, err)}
	}

	text := strings.TrimRight(string(data), "\n")
	return &text, nil
}

// exclusive refuses two flags given together where one of them would
// otherwise win without a word.
func exclusive(cmd *cobra.Command, pairs ...[2]string) error {
	for _, pair := range pairs {
		if cmd.Flags().Changed(pair[0]) && cmd.Flags().Changed(pair[1]) {
			return &config.UsageError{Message: fmt.Sprintf("--%s and --%s cannot be used together.", pair[0], pair[1])}
		}
	}
	return nil
}

// optional turns the flag package's zero value into "not given".
func optional(s string) *string {
	if s == "" {
		return nil
	}
	return &s
}

// noneOrValue is how a flag clears a field: `none` is JSON null, anything else
// the value, and an empty string is "not given".
func noneOrValue(s string) (given bool, value any) {
	switch s {
	case "":
		return false, nil
	case "none":
		return true, nil
	default:
		return true, s
	}
}
