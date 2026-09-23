package cmd

import (
	"context"
	"fmt"
	"math"
	"net/http"
	"time"

	"github.com/spf13/cobra"

	"github.com/datavisionzero/planaffe/src/cli/internal/api"
	"github.com/datavisionzero/planaffe/src/cli/internal/client"
	"github.com/datavisionzero/planaffe/src/cli/internal/render"
)

func newNeedsYou(g *globals) *cobra.Command {
	var (
		cursor string
		limit  int
		wait   int
	)
	cmd := &cobra.Command{
		Use:   "needs-you",
		Short: "What only a human can resolve: questions, review, unready work and stuck blocker chains.",
		Args:  cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			waiting, round, err := waitRound(cmd, wait)
			if err != nil {
				return err
			}
			cfg, c, err := g.loadForWait(round)
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
			resp, err := client.Checked(c.ListNeedsYouWithResponse(cmd.Context(), project, params))
			if err != nil {
				return err
			}
			page := resp.JSON200
			if waiting && len(page.Items) == 0 {
				etag := resp.HTTPResponse.Header.Get("ETag")
				remaining := wait
				for {
					seconds := serverRound(remaining)
					value := int32(seconds)
					params.Wait = &value
					params.IfNoneMatch = optional(etag)
					started := time.Now()
					resp, err = c.ListNeedsYouWithResponse(cmd.Context(), project, params)
					if err != nil {
						return client.Transport(err)
					}
					spent := seconds
					if resp.HTTPResponse.StatusCode != http.StatusNotModified {
						if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
							return err
						}
						page = resp.JSON200
						if len(page.Items) > 0 {
							break
						}
						// The ETag covers the whole page, the count of agents
						// included: an agent token created or revoked changes
						// it while the list stays empty. That is not something
						// needing a human, so the wait goes on from the new tag
						// for whatever is left of it.
						etag = resp.HTTPResponse.Header.Get("ETag")
						if spent, err = waited(cmd.Context(), started); err != nil {
							return client.Transport(err)
						}
					} else {
						etag = resp.HTTPResponse.Header.Get("ETag")
					}
					if remaining <= spent {
						if g.json {
							_ = render.JSON(cmd.OutOrStdout(), page)
						}
						return emptyResult{}
					}
					remaining -= spent
				}
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), page)
			}
			render.NeedsYou(cmd.OutOrStdout(), page.Items)
			if len(page.Items) == 0 {
				fmt.Fprintln(cmd.OutOrStdout(), "Nothing needs you.")
			}
			// A fact about the instance, said once beside the list rather than
			// once per ticket: without an agent nothing here gets worked off,
			// and the one thing to do about it is not on the list.
			if page.Agents == 0 {
				fmt.Fprintln(cmd.ErrOrStderr(), "This instance has no active agent token, so nothing will be worked off: pa agent create <name>.")
			}
			if page.HasMore && page.NextCursor != nil {
				fmt.Fprintf(cmd.ErrOrStderr(), "%d of %d; next page: --cursor %s\n", len(page.Items), page.Total, *page.NextCursor)
			}
			return nil
		},
	}
	cmd.Flags().StringVar(&cursor, "cursor", "", "the next page, as the previous one said")
	cmd.Flags().IntVar(&limit, "limit", 50, "1 to 200")
	cmd.Flags().IntVar(&wait, "wait", 0, "wait this many seconds until something needs a human")
	return cmd
}

// waited is how many whole seconds of the wait a round that came back early
// used up, and never less than one: a round answered at once is held until a
// second has gone by, so that an instance whose tag keeps moving is asked at
// most once a second and the wait still ends at its deadline.
func waited(ctx context.Context, started time.Time) (int, error) {
	elapsed := time.Since(started)
	if elapsed < time.Second {
		select {
		case <-ctx.Done():
			return 0, ctx.Err()
		case <-time.After(time.Second - elapsed):
		}
		return 1, nil
	}
	return int(math.Ceil(elapsed.Seconds())), nil
}
