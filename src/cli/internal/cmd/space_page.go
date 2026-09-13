package cmd

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"strings"

	"github.com/spf13/cobra"

	"github.com/datavisionzero/planaffe/src/cli/internal/api"
	"github.com/datavisionzero/planaffe/src/cli/internal/client"
	"github.com/datavisionzero/planaffe/src/cli/internal/config"
	"github.com/datavisionzero/planaffe/src/cli/internal/render"
)

// address is a page of the knowledge base as it is written on a command line:
// the space, then the slugs from the root down, separated by slashes —
// `handbuch/company/onboarding`. It is the address of ADR 0028 and the address
// in the browser's location bar, spelled once and the same way everywhere.
//
// The command line takes it as one argument rather than as two, although the
// contract separates the space from the path. An address is a thing a reader
// copies out of a tree, a search hit or a sentence, and splitting it in two
// would mean editing it before it can be used.
type address struct {
	space string
	path  string
}

// newSpacePage is what a person does with a page of the knowledge base. It is
// `pa space page` and not `pa page` because `pa page` is the project's wiki
// while that exists; when the wiki is withdrawn (VISION 18) the word comes
// free and this moves there.
func newSpacePage(g *globals) *cobra.Command {
	cmd := &cobra.Command{
		Use:   "page",
		Short: "Pages in a space: the knowledge base's Markdown, addressed by the slugs from the space down.",
	}
	cmd.AddCommand(newSpacePageList(g), newSpacePageView(g), newSpacePageCreate(g), newSpacePageEdit(g), newSpacePageRename(g), newSpacePageMove(g), newSpacePageDelete(g), newSpacePageRestore(g))
	return cmd
}

// parseAddress splits at the first slash: the space in front, the path behind.
// A word without a slash names a space and not a page, which is a mistake the
// command line already shows — so it is a usage error and the instance never
// hears about it.
func parseAddress(arg string) (address, error) {
	space, path, found := strings.Cut(arg, "/")
	if !found || space == "" || path == "" {
		return address{}, &config.UsageError{
			Message: "an address is the space and the slugs under it: handbuch/company/onboarding.",
		}
	}
	return address{space: space, path: path}, nil
}

// under splits an address into where the page hangs and what it is called: the
// last slug is the page's own, everything in front of it is its parent. A
// parent of "" is the space itself, which is where a page at the top hangs.
func (a address) under() (parent, slug string) {
	if i := strings.LastIndex(a.path, "/"); i >= 0 {
		return a.path[:i], a.path[i+1:]
	}
	return "", a.path
}

func printSpacePage(g *globals, cmd *cobra.Command, page api.SpacePage) error {
	if g.json {
		return render.JSON(cmd.OutOrStdout(), page)
	}
	render.SpacePage(cmd.OutOrStdout(), page)
	return nil
}

// newSpacePageList is the tree, slim and without the bodies — the sibling of
// `pa issue list` (ADR 0012). It takes a space and not an address: a tree is
// not a page.
func newSpacePageList(g *globals) *cobra.Command {
	return &cobra.Command{
		Use: "list SPACE", Short: "The tree of a space: a page, then everything under it, without the bodies.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.ListSpacePagesWithResponse(cmd.Context(), args[0])
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON200)
			}
			if len(*resp.JSON200) == 0 {
				fmt.Fprintf(cmd.OutOrStdout(), "No pages in %s yet; `pa space page create %s/<slug> --title …` writes the first.\n", args[0], args[0])
				return nil
			}
			render.SpaceTree(cmd.OutOrStdout(), *resp.JSON200)
			return nil
		},
	}
}

func newSpacePageView(g *globals) *cobra.Command {
	return &cobra.Command{
		Use: "view ADDRESS", Short: "The page: the head, then the Markdown as it is stored.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			at, err := parseAddress(args[0])
			if err != nil {
				return err
			}
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.ReadSpacePageWithResponse(cmd.Context(), at.space, at.path, client.ByAddress)
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			return printSpacePage(g, cmd, *resp.JSON200)
		},
	}
}

// newSpacePageCreate takes the address the page is to have, and reads the tree
// out of it: the last slug is the page's, everything in front is its parent.
// One argument says both where it hangs and what it is called, and it says it
// in the spelling the page will be reached by afterwards.
func newSpacePageCreate(g *globals) *cobra.Command {
	var title, bodyFile string
	cmd := &cobra.Command{
		Use: "create ADDRESS --title TITLE", Short: "Create a page at that address; the slug is given, never derived from the title.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			at, err := parseAddress(args[0])
			if err != nil {
				return err
			}
			if title == "" {
				return &config.UsageError{Message: "a page has a title: --title."}
			}
			body, err := readText(cmd.InOrStdin(), bodyFile)
			if err != nil {
				return err
			}
			_, c, err := g.load()
			if err != nil {
				return err
			}

			parent, slug := at.under()
			request := api.CreateSpacePageBody{Slug: &slug, Title: &title, Body: body, Parent: optional(parent)}
			resp, err := c.CreateSpacePageWithResponse(cmd.Context(), at.space, request)
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			return printSpacePage(g, cmd, *resp.JSON201)
		},
	}
	cmd.Flags().StringVar(&title, "title", "", "the one line that says what the page is")
	cmd.Flags().StringVar(&bodyFile, "body-file", "", "the Markdown, from a file or `-` for stdin")
	return cmd
}

// newSpacePageEdit changes the text and nothing about where the page stands.
// There is no --label here and that is not an oversight: a page in a space
// carries none, because a label is defined per project and a space has none.
func newSpacePageEdit(g *globals) *cobra.Command {
	var title, bodyFile, ifMatch string
	cmd := &cobra.Command{
		Use: "edit ADDRESS", Short: "Change the title or the Markdown; --if-match guards the document.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			at, err := parseAddress(args[0])
			if err != nil {
				return err
			}
			changes := map[string]any{}
			if title != "" {
				changes["title"] = title
			}
			body, err := readText(cmd.InOrStdin(), bodyFile)
			if err != nil {
				return err
			}
			if body != nil {
				changes["body"] = *body
			}
			if len(changes) == 0 {
				return &config.UsageError{Message: "nothing to change: --title or --body-file. The slug is `pa space page rename`, the parent `pa space page move`."}
			}
			return changeSpacePage(g, cmd, at, changes, ifMatch)
		},
	}
	cmd.Flags().StringVar(&title, "title", "", "the new title")
	cmd.Flags().StringVar(&bodyFile, "body-file", "", "the Markdown, from a file or `-` for stdin")
	cmd.Flags().StringVar(&ifMatch, "if-match", "", "the updated_at as last read; refused as stale when it moved")
	return cmd
}

// newSpacePageRename is its own verb rather than a flag on `edit`, because
// moving an address is not the same kind of act as editing a text: nothing
// forwards, and with a page the address of every page under it moves as well
// (ADR 0021, ADR 0028).
//
// It takes a slug and not an address. Where a page hangs is `move`; what it is
// called at that place is this.
func newSpacePageRename(g *globals) *cobra.Command {
	var ifMatch string
	cmd := &cobra.Command{
		Use: "rename ADDRESS NEW-SLUG", Short: "Rename the page where it stands. Nothing forwards; the old address leads nowhere.", Args: cobra.ExactArgs(2),
		RunE: func(cmd *cobra.Command, args []string) error {
			at, err := parseAddress(args[0])
			if err != nil {
				return err
			}
			if strings.Contains(args[1], "/") {
				return &config.UsageError{Message: "a rename takes a slug, not an address: where the page hangs is `pa space page move`."}
			}
			return changeSpacePage(g, cmd, at, map[string]any{"slug": args[1]}, ifMatch)
		},
	}
	cmd.Flags().StringVar(&ifMatch, "if-match", "", "the updated_at as last read; refused as stale when it moved")
	return cmd
}

func changeSpacePage(g *globals, cmd *cobra.Command, at address, changes map[string]any, ifMatch string) error {
	_, c, err := g.load()
	if err != nil {
		return err
	}
	body, _ := json.Marshal(changes)
	resp, err := c.ChangeSpacePageWithBodyWithResponse(cmd.Context(), at.space, at.path, "application/json", bytes.NewReader(body),
		client.ByAddress,
		func(_ context.Context, req *http.Request) error {
			if ifMatch != "" {
				req.Header.Set("If-Match", `"`+strings.Trim(ifMatch, `"`)+`"`)
			}
			return nil
		})
	if err != nil {
		return client.Transport(err)
	}
	if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
		return err
	}
	return printSpacePage(g, cmd, *resp.JSON200)
}

// newSpacePageMove hangs the page under another parent, with everything below
// it. It is a step of its own rather than a field of `edit` because it rewrites
// the depth of the whole subtree and has refusals no other change has — under
// itself, past the third level, onto a slug that is taken where it would land.
// That is the line ADR 0016 draws for the status, applied to the tree.
//
// One flag covers both fields of the contract. `--under` takes the same
// address form as everything else here — `handbuch/product` is a page, and a
// word without a slash is a space, `archive` meaning "directly under the space
// archive". Left out, the page lands directly under the space it is already
// in, which is the way up and wants no second spelling.
func newSpacePageMove(g *globals) *cobra.Command {
	var under string
	cmd := &cobra.Command{
		Use: "move ADDRESS [--under TARGET]", Short: "Hang the page under another parent, in this space or another one, with everything below it.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			at, err := parseAddress(args[0])
			if err != nil {
				return err
			}

			request := api.MoveSpacePageBody{Path: &at.path}
			if under != "" {
				space, path, found := strings.Cut(under, "/")
				if space == "" || (found && path == "") {
					return &config.UsageError{
						Message: "--under is a page by its address, or a space by its name: handbuch/product, or archive.",
					}
				}
				request.Space = &space
				request.Parent = optional(path)
			}

			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.MoveSpacePageWithResponse(cmd.Context(), at.space, request)
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			return printSpacePage(g, cmd, *resp.JSON200)
		},
	}
	cmd.Flags().StringVar(&under, "under", "", "the new parent by its address, or a space by its name; absent means directly under the space it is in")
	return cmd
}

// newSpacePageDelete takes the subtree with it and says how many went. A
// caller who asked about one page has to learn that three are gone, which is
// why the route answers a count rather than nothing at all.
func newSpacePageDelete(g *globals) *cobra.Command {
	return &cobra.Command{
		Use: "delete ADDRESS", Short: "Soft-delete a page and every page under it; the addresses stay spent until the purge.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			at, err := parseAddress(args[0])
			if err != nil {
				return err
			}
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.DeleteSpacePageWithResponse(cmd.Context(), at.space, at.path, client.ByAddress)
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON200)
			}
			fmt.Fprintln(cmd.OutOrStdout(), deleted(args[0], int(resp.JSON200.Deleted)))
			return nil
		},
	}
}

// deleted is the sentence after a delete. The count is in it whenever it is
// more than one, because a caller who asked about one page has to learn that
// three are gone — and the route answers a number rather than nothing for
// exactly that reason.
func deleted(at string, many int) string {
	if many > 1 {
		return fmt.Sprintf("%s deleted with the %d pages under it; `pa space page restore %s` brings them back.", at, many-1, at)
	}
	return fmt.Sprintf("%s deleted; `pa space page restore %s` brings it back.", at, at)
}

// newSpacePageRestore brings back exactly what went along. A page whose parent
// is still deleted is refused with the address to restore first: a live page
// under a deleted one is the state the rule exists to prevent.
func newSpacePageRestore(g *globals) *cobra.Command {
	return &cobra.Command{
		Use: "restore ADDRESS", Short: "Bring a deleted page back, with exactly the pages that went with it.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			at, err := parseAddress(args[0])
			if err != nil {
				return err
			}
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.RestoreSpacePageWithResponse(cmd.Context(), at.space, api.RestoreSpacePageBody{Path: &at.path})
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			return printSpacePage(g, cmd, *resp.JSON200)
		},
	}
}
