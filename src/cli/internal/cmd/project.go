package cmd

import (
	"bytes"
	"encoding/json"
	"fmt"
	"strings"

	"github.com/spf13/cobra"

	"github.com/datavisionzero/planaffe/src/cli/internal/api"
	"github.com/datavisionzero/planaffe/src/cli/internal/client"
	"github.com/datavisionzero/planaffe/src/cli/internal/config"
	"github.com/datavisionzero/planaffe/src/cli/internal/render"
)

func newProject(g *globals) *cobra.Command {
	cmd := &cobra.Command{Use: "project", Short: "Projects: the bracket everything belongs to, and its two switches."}
	cmd.AddCommand(newProjectCreate(g), newProjectList(g), newProjectView(g), newProjectEdit(g), newProjectDelete(g), newProjectRestore(g))
	return cmd
}

func printProject(g *globals, cmd *cobra.Command, project api.Project) error {
	if g.json {
		return render.JSON(cmd.OutOrStdout(), project)
	}
	render.Project(cmd.OutOrStdout(), project)
	return nil
}

func newProjectCreate(g *globals) *cobra.Command {
	var triage, review bool
	cmd := &cobra.Command{
		Use:   "create KEY NAME",
		Short: "Create a project: the key that prefixes everything in it, typed by a person and never changed.",
		Args:  cobra.ExactArgs(2),
		RunE: func(cmd *cobra.Command, args []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			key := strings.ToUpper(args[0])
			body := api.CreateProjectRequest{Key: &key, Name: &args[1]}
			if triage {
				body.TriageRequired = &triage
			}
			if review {
				body.ReviewRequired = &review
			}
			resp, err := c.CreateProjectWithResponse(cmd.Context(), body)
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			return printProject(g, cmd, *resp.JSON201)
		},
	}
	cmd.Flags().BoolVar(&triage, "triage-required", false, "ready becomes a human's word and binding for next")
	cmd.Flags().BoolVar(&review, "review-required", false, "every close by an agent lands in review")
	return cmd
}

func newProjectList(g *globals) *cobra.Command {
	return &cobra.Command{
		Use: "list", Short: "Every project.", Args: cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.ListProjectsWithResponse(cmd.Context())
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON200)
			}
			for _, p := range *resp.JSON200 {
				render.ProjectLine(cmd.OutOrStdout(), p)
			}
			return nil
		},
	}
}

func newProjectView(g *globals) *cobra.Command {
	return &cobra.Command{
		Use: "view [KEY]", Short: "One project; the repository's own when no key is given.", Args: cobra.MaximumNArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			cfg, c, err := g.load()
			if err != nil {
				return err
			}
			key, err := projectArg(cfg, args)
			if err != nil {
				return err
			}
			resp, err := c.ReadProjectWithResponse(cmd.Context(), key)
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			return printProject(g, cmd, *resp.JSON200)
		},
	}
}

func projectArg(cfg config.Config, args []string) (string, error) {
	if len(args) > 0 {
		return strings.ToUpper(args[0]), nil
	}
	return requireProject(cfg)
}

func newProjectEdit(g *globals) *cobra.Command {
	var name, triage, review string
	cmd := &cobra.Command{
		Use: "edit KEY", Short: "Change the name or the switches; the key is immutable.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			changes := map[string]any{}
			if name != "" {
				changes["name"] = name
			}
			for flag, value := range map[string]string{"triage_required": triage, "review_required": review} {
				switch value {
				case "":
				case "true":
					changes[flag] = true
				case "false":
					changes[flag] = false
				default:
					return &config.UsageError{Message: fmt.Sprintf("--%s is true or false.", strings.ReplaceAll(flag, "_", "-"))}
				}
			}
			if len(changes) == 0 {
				return &config.UsageError{Message: "nothing to change: --name, --triage-required or --review-required."}
			}
			body, _ := json.Marshal(changes)
			resp, err := c.ChangeProjectWithBodyWithResponse(cmd.Context(), strings.ToUpper(args[0]), "application/json", bytes.NewReader(body))
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			return printProject(g, cmd, *resp.JSON200)
		},
	}
	cmd.Flags().StringVar(&name, "name", "", "the new name")
	cmd.Flags().StringVar(&triage, "triage-required", "", "true or false")
	cmd.Flags().StringVar(&review, "review-required", "", "true or false")
	return cmd
}

func newProjectDelete(g *globals) *cobra.Command {
	var confirm string
	cmd := &cobra.Command{
		Use:   "delete KEY --confirm KEY",
		Short: "Soft-delete the project with everything in it. Administrators only; the key typed twice, never prompted for.",
		Args:  cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			key := strings.ToUpper(args[0])
			if !strings.EqualFold(confirm, key) {
				return &config.UsageError{Message: fmt.Sprintf("deleting %s takes everything in it with it; pass --confirm %s to mean it.", key, key)}
			}
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.DeleteProjectWithResponse(cmd.Context(), key)
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), map[string]any{"key": key, "deleted": true})
			}
			fmt.Fprintf(cmd.OutOrStdout(), "%s deleted with everything in it; `pa project restore %s` brings it back within the grace period.\n", key, key)
			return nil
		},
	}
	cmd.Flags().StringVar(&confirm, "confirm", "", "the key again, to mean it")
	return cmd
}

func newProjectRestore(g *globals) *cobra.Command {
	return &cobra.Command{
		Use: "restore KEY", Short: "Bring a deleted project back, with everything in it.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.RestoreProjectWithResponse(cmd.Context(), strings.ToUpper(args[0]))
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			return printProject(g, cmd, *resp.JSON200)
		},
	}
}
