package config

import (
	"bufio"
	"fmt"
	"os"
	"path/filepath"
	"strings"
)

// Settings is the only state pa keeps between invocations (ADR 0025): which
// instance this machine logged in to, and — where one was chosen — the *path*
// of a token file. Nothing in it is a credential, and nothing in it is about a
// repository; that is what `.planaffe` is for.
type Settings struct {
	Instance  string
	TokenFile string
}

// SettingsName is the file inside the configuration directory.
const SettingsName = "config"

// SettingsPath is `$XDG_CONFIG_HOME/planaffe/config`, or `~/.config/planaffe/config`
// where the variable is unset — the place a console-minded user expects it and
// the one `pa login` writes.
func SettingsPath(getenv func(string) string) (string, error) {
	if getenv == nil {
		getenv = os.Getenv
	}

	if base := strings.TrimSpace(getenv("XDG_CONFIG_HOME")); base != "" {
		return filepath.Join(base, "planaffe", SettingsName), nil
	}

	home := strings.TrimSpace(getenv("HOME"))
	if home == "" {
		var err error
		if home, err = os.UserHomeDir(); err != nil {
			return "", &UsageError{fmt.Sprintf(
				"pa could not find a configuration directory: set XDG_CONFIG_HOME or HOME. (%v)", err)}
		}
	}

	return filepath.Join(home, ".config", "planaffe", SettingsName), nil
}

// ReadSettings reads the file. A file that is not there is not an error: it is
// a machine nobody has logged in on, which is the ordinary state of one that
// works from PLANAFFE_TOKEN.
func ReadSettings(path string) (Settings, error) {
	file, err := os.Open(path)
	if err != nil {
		if os.IsNotExist(err) {
			return Settings{}, nil
		}
		return Settings{}, &UsageError{fmt.Sprintf("%s could not be read: %v", path, err)}
	}
	defer file.Close()

	var settings Settings
	scanner := bufio.NewScanner(file)
	line := 0
	for scanner.Scan() {
		line++
		text := strings.TrimSpace(scanner.Text())
		if text == "" || strings.HasPrefix(text, "#") {
			continue
		}

		key, value, ok := strings.Cut(text, "=")
		if !ok {
			return Settings{}, &UsageError{fmt.Sprintf("%s line %d: expected `key = value`", path, line)}
		}

		key, value = strings.TrimSpace(key), strings.TrimSpace(value)
		switch key {
		case "instance":
			settings.Instance = value
		case "token_file":
			settings.TokenFile = value
		default:
			return Settings{}, &UsageError{fmt.Sprintf(
				"%s line %d: unknown key %q; the file knows `instance` and `token_file`", path, line, key)}
		}
	}
	if err := scanner.Err(); err != nil {
		return Settings{}, &UsageError{fmt.Sprintf("%s could not be read: %v", path, err)}
	}

	return settings, nil
}

// WriteSettings writes it, creating the directory if it is not there.
func WriteSettings(path string, settings Settings) error {
	if err := os.MkdirAll(filepath.Dir(path), 0o700); err != nil {
		return &UsageError{fmt.Sprintf("%s could not be created: %v", filepath.Dir(path), err)}
	}

	var out strings.Builder
	out.WriteString("# pa's own configuration. Written by `pa login`; no credential is in it.\n")
	if settings.Instance != "" {
		fmt.Fprintf(&out, "instance = %s\n", settings.Instance)
	}
	if settings.TokenFile != "" {
		fmt.Fprintf(&out, "token_file = %s\n", settings.TokenFile)
	}

	if err := os.WriteFile(path, []byte(out.String()), 0o600); err != nil {
		return &UsageError{fmt.Sprintf("%s could not be written: %v", path, err)}
	}
	return nil
}

// ReadTokenFile reads a token out of the file the user chose over the keychain,
// and refuses one anybody else on the machine can read. A file mode is the only
// protection a token in a file has; shrugging at 0644 would be the quiet
// fallback to plaintext ADR 0025 refuses to make.
func ReadTokenFile(path string) (string, error) {
	info, err := os.Stat(path)
	if err != nil {
		return "", &UsageError{fmt.Sprintf("%s holds this machine's token and could not be read: %v", path, err)}
	}
	if mode := info.Mode().Perm(); mode&0o077 != 0 {
		return "", &UsageError{fmt.Sprintf(
			"%s is readable by others (mode %04o). A token in a file is protected by nothing else: `chmod 600 %s`.",
			path, mode, path)}
	}

	content, err := os.ReadFile(path)
	if err != nil {
		return "", &UsageError{fmt.Sprintf("%s could not be read: %v", path, err)}
	}

	token := strings.TrimSpace(string(content))
	if token == "" {
		return "", &UsageError{fmt.Sprintf("%s is empty: run `pa login --token-file %s`.", path, path)}
	}
	return token, nil
}

// WriteTokenFile writes one, readable by its owner and nobody else.
//
// The mode of os.WriteFile applies only to a file it creates: over an existing
// 0644 file it would have left the token readable by everybody, and the next
// command would have refused the file it had just been told was private. So
// the token goes into a fresh file beside the old one — created 0600, chmod-ed
// once more against an odd umask — and is renamed over it, which also means no
// reader ever sees half a token. A path that is there and is not a regular
// file is refused rather than replaced.
func WriteTokenFile(path, token string) error {
	if err := CanHoldToken(path); err != nil {
		return err
	}

	dir := filepath.Dir(path)
	if err := os.MkdirAll(dir, 0o700); err != nil {
		return &UsageError{fmt.Sprintf("%s could not be created: %v", dir, err)}
	}

	file, err := os.CreateTemp(dir, "."+filepath.Base(path)+".*")
	if err != nil {
		return &UsageError{fmt.Sprintf("%s could not be written: %v", path, err)}
	}
	temp := file.Name()
	written := false
	defer func() {
		if !written {
			_ = os.Remove(temp)
		}
	}()

	if err := file.Chmod(0o600); err != nil {
		file.Close()
		return &UsageError{fmt.Sprintf("%s could not be written: %v", path, err)}
	}
	if _, err := file.WriteString(token + "\n"); err != nil {
		file.Close()
		return &UsageError{fmt.Sprintf("%s could not be written: %v", path, err)}
	}
	if err := file.Sync(); err != nil {
		file.Close()
		return &UsageError{fmt.Sprintf("%s could not be written: %v", path, err)}
	}
	if err := file.Close(); err != nil {
		return &UsageError{fmt.Sprintf("%s could not be written: %v", path, err)}
	}
	if err := os.Rename(temp, path); err != nil {
		return &UsageError{fmt.Sprintf("%s could not be written: %v", path, err)}
	}
	written = true
	return nil
}

// CanHoldToken refuses a path that is there and is not a regular file — a
// directory, a device, a symbolic link the rename would silently replace.
// Nothing there yet is fine: WriteTokenFile creates it.
func CanHoldToken(path string) error {
	if info, err := os.Lstat(path); err == nil && !info.Mode().IsRegular() {
		return &UsageError{fmt.Sprintf("%s is not a regular file; pa writes a token only to one.", path)}
	}
	return nil
}

// AbsolutePath makes a path the user typed into the one it means wherever the
// next command runs: a leading `~/` is the home directory — a shell leaves it
// alone inside quotes or after `=` — and a relative path is read against dir,
// the directory it was typed in. Stored as typed, it would have been read
// against whatever directory the next command happened to start in.
func AbsolutePath(path, dir, home string) (string, error) {
	if path == "~" || strings.HasPrefix(path, "~/") {
		if home == "" {
			return "", &UsageError{fmt.Sprintf("%s starts with ~, and pa could not find the home directory: set HOME.", path)}
		}
		path = filepath.Join(home, strings.TrimPrefix(path, "~"))
	}
	if !filepath.IsAbs(path) {
		path = filepath.Join(dir, path)
	}
	return filepath.Clean(path), nil
}
