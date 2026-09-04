package cmd

import (
	"fmt"

	"github.com/spf13/cobra"

	"github.com/datavisionzero/planaffe/src/cli/internal/api"
	"github.com/datavisionzero/planaffe/src/cli/internal/client"
	"github.com/datavisionzero/planaffe/src/cli/internal/render"
)

func newNeedsYou(g *globals) *cobra.Command {
	var (
		cursor string
		limit  int
	)
	cmd := &cobra.Command{
		Use:   "needs-you",
		Short: "What only a human can resolve: questions, review, unready work and stuck blocker chains.",
		Args:  cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			cfg, c, err := g.load()
			if err != nil {
				return err
			}
			project, err := requireProject(cfg)
			if err != nil {
				return err
			}

			params := &api.ListNeedsYouParams{Cursor: optional(cursor)}
			if cmd.Flags().Changed("limit") {
				value := int32(limit)
				params.Limit = &value
			}
			resp, err := c.ListNeedsYouWithResponse(cmd.Context(), project, params)
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON200)
			}
			render.NeedsYou(cmd.OutOrStdout(), resp.JSON200.Items)
			if resp.JSON200.HasMore && resp.JSON200.NextCursor != nil {
				fmt.Fprintf(cmd.ErrOrStderr(), "%d of %d; next page: --cursor %s\n", len(resp.JSON200.Items), resp.JSON200.Total, *resp.JSON200.NextCursor)
			}
			return nil
		},
	}
	cmd.Flags().StringVar(&cursor, "cursor", "", "the next page, as the previous one said")
	cmd.Flags().IntVar(&limit, "limit", 50, "1 to 200")
	return cmd
}
