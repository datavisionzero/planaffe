package cmd

import (
	"github.com/spf13/cobra"

	"github.com/datavisionzero/planaffe/src/cli/internal/client"
	"github.com/datavisionzero/planaffe/src/cli/internal/render"
)

// newStanding is the overview on the console: one line per project, worst
// first. It is the one command that looks across projects, so it takes no key
// and reads no `.planaffe` file — everything the caller can see is the point.
func newStanding(g *globals) *cobra.Command {
	return &cobra.Command{
		Use:   "standing",
		Short: "How every project you can see is standing, worst first.",
		Args:  cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.ReadStandingWithResponse(cmd.Context())
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON200)
			}
			render.Standing(cmd.OutOrStdout(), *resp.JSON200)
			return nil
		},
	}
}
