package cmd

import (
	"fmt"
	"strings"

	"github.com/spf13/cobra"

	"github.com/datavisionzero/planaffe/src/cli/internal/api"
	"github.com/datavisionzero/planaffe/src/cli/internal/client"
	"github.com/datavisionzero/planaffe/src/cli/internal/config"
	"github.com/datavisionzero/planaffe/src/cli/internal/render"
)

// newSpace is the knowledge base on the console. It carries one verb today —
// the search — and the rest of what a person does with a space and its pages
// hangs under it later.
//
// The word is `space` and not `page` on purpose. `pa page` is the project's
// wiki and points somewhere else entirely; a `pa page search` that answered
// about the knowledge base while `pa page list` answered about a project would
// be the one ambiguity this product must not have. A page of the knowledge
// base hangs on a space, so it is reached through one.
func newSpace(g *globals) *cobra.Command {
	cmd := &cobra.Command{
		Use:   "space",
		Short: "Spaces: the knowledge base's bracket, and the search across the pages in it.",
	}
	cmd.AddCommand(newSpaceSearch(g))
	return cmd
}

// newSpaceSearch is the one search across the knowledge base (VISION 18). It
// is not the ticket search and never mixes with it: whoever searches the
// knowledge base is asking a different question.
func newSpaceSearch(g *globals) *cobra.Command {
	var space string
	var limit int
	cmd := &cobra.Command{
		Use:   `search QUERY`,
		Short: "Full-text search over the titles and bodies of the pages, best first, in the spaces you may see.",
		Long: "Full-text search over the titles and bodies of the knowledge base's pages, best first.\n\n" +
			"QUERY takes what a search box takes: words, \"a phrase\", -not. A space you cannot see is\n" +
			"not-found, which is the answer a space that does not exist gives.",
		Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			query := strings.TrimSpace(args[0])
			if query == "" {
				// The instance would refuse it as well; asking it first would
				// be a round trip to learn what the command line already says.
				return &config.UsageError{Message: "a search is a search for something: pa space search QUERY."}
			}

			_, c, err := g.load()
			if err != nil {
				return err
			}

			params := &api.SearchPagesParams{Q: &query, Space: optional(space)}
			if limit > 0 {
				asked := int32(limit)
				params.Limit = &asked
			}

			resp, err := c.SearchPagesWithResponse(cmd.Context(), params)
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON200)
			}
			// Nothing found is an answer and says so, the way `needs-you` does.
			// Under --json it is the empty list and nothing else: that is what
			// an agent parses.
			if len(*resp.JSON200) == 0 {
				fmt.Fprintf(cmd.OutOrStdout(), "Nothing matched %q.\n", query)
				return nil
			}

			// The pieces say what matched; whether they are drawn as anything
			// but text is a question about where the output is going.
			render.Hits(cmd.OutOrStdout(), *resp.JSON200, render.Terminal(cmd.OutOrStdout()) && g.getenv("NO_COLOR") == "")
			return nil
		},
	}
	cmd.Flags().StringVar(&space, "space", "", "one space by name; the whole knowledge base where it is absent")
	cmd.Flags().IntVar(&limit, "limit", 0, "how many hits at most, 1 to 100; the instance decides where it is absent")
	return cmd
}
