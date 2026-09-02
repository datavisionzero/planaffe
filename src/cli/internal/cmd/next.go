package cmd

import (
	"context"
	"net/http"

	"github.com/spf13/cobra"

	"github.com/datavisionzero/planaffe/src/cli/internal/api"
	"github.com/datavisionzero/planaffe/src/cli/internal/client"
	"github.com/datavisionzero/planaffe/src/cli/internal/render"
)

// emptyNext is the one exit code that is not an error of the API: `next` found
// nothing, and a loop branches on it (docs/api.md).
type emptyNext struct{}

func (emptyNext) Error() string { return "nothing workable" }

func newNext(g *globals) *cobra.Command {
	var (
		claim  bool
		ready  bool
		epic   string
		labels []string
		repo   string
	)

	cmd := &cobra.Command{
		Use:   "next",
		Short: "What the caller would be handed, in that order — or, with --claim, take the first and claim it in one act.",
		Long: `Without --claim, the "ready for agents" list of the project: every workable
issue for the caller, priority first, then an epic nobody else is working in,
then age (VISION 10). With --claim, the act at the centre of the product: the
highest-ranked workable issue is handed out and claimed in one transaction that
cannot be split. Exit 8 when nothing is workable, with the reasons.`,
		Args: cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			cfg, c, err := g.load()
			if err != nil {
				return err
			}
			project, err := requireProject(cfg)
			if err != nil {
				return err
			}

			// The repo label comes from the .planaffe file unless told
			// otherwise; `--repo none` asks for every issue of the project.
			repoLabel := cfg.Repo
			if cmd.Flags().Changed("repo") {
				repoLabel = repo
			}
			if repoLabel == "none" {
				repoLabel = ""
			}

			var (
				readyPtr *bool
				epicPtr  *string
				repoPtr  *string
				labelPtr *[]string
			)
			if ready {
				readyPtr = &ready
			}
			if epic != "" {
				epicPtr = &epic
			}
			if repoLabel != "" {
				repoPtr = &repoLabel
			}
			if len(labels) > 0 {
				labelPtr = &labels
			}

			ctx := cmd.Context()
			out := cmd.OutOrStdout()

			if !claim {
				// The generated params carry no `label`; the query is built by
				// the editor so that `label` repeats the way the API reads it.
				resp, err := c.PreviewNextWithResponse(ctx, project, &api.PreviewNextParams{Ready: readyPtr, Epic: epicPtr, Repo: repoPtr},
					withLabels(labels))
				if err != nil {
					return client.Transport(err)
				}
				if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
					return err
				}
				if g.json {
					return render.JSON(out, resp.JSON200)
				}
				render.Summaries(out, resp.JSON200.Items)
				if len(resp.JSON200.Items) == 0 {
					render.Reasons(out, resp.JSON200.Reasons)
					return emptyNext{}
				}
				return nil
			}

			resp, err := c.TakeNextWithResponse(ctx, project, api.NextRequest{Ready: readyPtr, Epic: epicPtr, Repo: repoPtr, Label: labelPtr})
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}

			if g.json {
				if err := render.JSON(out, resp.JSON200); err != nil {
					return err
				}
			} else if resp.JSON200.Issue != nil {
				render.Issue(out, *resp.JSON200.Issue)
			} else {
				render.Reasons(out, resp.JSON200.Reasons)
			}

			if resp.JSON200.Issue == nil {
				return emptyNext{}
			}
			return nil
		},
	}

	cmd.Flags().BoolVar(&claim, "claim", false, "take the highest-ranked workable issue and claim it")
	cmd.Flags().BoolVar(&ready, "ready", false, "only flagged issues, even where triage is not required")
	cmd.Flags().StringVar(&epic, "epic", "", "only this epic's issues, e.g. PLAN-E3")
	cmd.Flags().StringArrayVar(&labels, "label", nil, "only issues carrying this label; repeatable, all must match")
	cmd.Flags().StringVar(&repo, "repo", "", "the `repo` label of this repository, or `none`; defaults to the .planaffe file")

	return cmd
}

// withLabels adds `label=` once per label to a GET, which the generated
// parameters cannot express as an array.
func withLabels(labels []string) api.RequestEditorFn {
	return func(_ context.Context, req *http.Request) error {
		if len(labels) == 0 {
			return nil
		}
		q := req.URL.Query()
		for _, l := range labels {
			q.Add("label", l)
		}
		req.URL.RawQuery = q.Encode()
		return nil
	}
}
