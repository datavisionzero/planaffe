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

// newSpace is the bracket of the knowledge base on the console: what brackets
// there are, one of them whole, and the search across them. The pages
// themselves are `pa page` and hang at the top level beside it, now that the
// project's wiki no longer holds that word (VISION 18).
//
// What is not here is what an agent may not do: creating a space, renaming it,
// the switch, deleting it and granting access to it. Those are a human's acts
// and they stay in the browser for now — the CLI does not mirror the whole
// interface here, and the border is the bracket rather than the work in it
// (VISION 18, ADR 0027).
func newSpace(g *globals) *cobra.Command {
	cmd := &cobra.Command{
		Use:   "space",
		Short: "Spaces: the knowledge base's bracket and the search across it.",
	}
	cmd.AddCommand(newSpaceList(g), newSpaceView(g), newSpaceSearch(g))
	return cmd
}

// newSpaceList is the knowledge base seen from outside: what brackets there
// are. Nothing is filtered here — a space the caller may not see is absent
// from the answer, and one closed to an agent is absent from an agent's
// answer, which is the same absence for the same reason (ADR 0027).
func newSpaceList(g *globals) *cobra.Command {
	return &cobra.Command{
		Use: "list", Short: "Every space you may see, by name. What is not in it is not there for you.", Args: cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			// No project: the knowledge base has none, so nothing here asks
			// for a `.planaffe` or for --project.
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := client.Checked(c.ListSpacesWithResponse(cmd.Context()))
			if err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON200)
			}
			if len(*resp.JSON200) == 0 {
				fmt.Fprintln(cmd.OutOrStdout(), "No spaces here. A space is a human's to create.")
				return nil
			}
			render.Spaces(cmd.OutOrStdout(), *resp.JSON200)
			return nil
		},
	}
}

// newSpaceView is one space complete: the head, and the tree of what stands in
// it. That is the division ADR 0012 draws for the issue, a second time — a
// list stays slim, a single object is whole — and the tree is what makes this
// one whole, because a space is otherwise four lines of head.
//
// Under --json it is the space as the route answered it and the tree is not
// fetched at all: two objects under one flag would be a shape of pa's own
// invention, and the tree has its own verb in `pa page list`.
func newSpaceView(g *globals) *cobra.Command {
	return &cobra.Command{
		Use: "view NAME", Short: "One space: the head, and the tree of the pages in it.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			space, err := client.Checked(c.ReadSpaceWithResponse(cmd.Context(), args[0]))
			if err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), space.JSON200)
			}

			tree, err := client.Checked(c.ListPagesWithResponse(cmd.Context(), args[0]))
			if err != nil {
				return err
			}

			out := cmd.OutOrStdout()
			render.Space(out, *space.JSON200)
			// A space with nothing in it says so rather than ending on its
			// head, the way `pa needs-you` answers an empty list.
			if len(*tree.JSON200) == 0 {
				fmt.Fprintf(out, "\nNo pages in %s yet; `pa page create %s/<slug> --title …` writes the first.\n", args[0], args[0])
				return nil
			}
			fmt.Fprintln(out)
			render.SpaceTree(out, *tree.JSON200)
			return nil
		},
	}
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

			resp, err := client.Checked(c.SearchPagesWithResponse(cmd.Context(), params))
			if err != nil {
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
