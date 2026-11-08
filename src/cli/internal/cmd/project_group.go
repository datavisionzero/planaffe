package cmd

import (
	"fmt"
	"github.com/datavisionzero/planaffe/src/cli/internal/api"
	"github.com/datavisionzero/planaffe/src/cli/internal/client"
	"github.com/datavisionzero/planaffe/src/cli/internal/config"
	"github.com/datavisionzero/planaffe/src/cli/internal/render"
	"github.com/google/uuid"
	"github.com/spf13/cobra"
)

func newProjectGroup(g *globals) *cobra.Command {
	root := &cobra.Command{Use: "project-group", Short: "Flat groups for project navigation."}
	root.AddCommand(&cobra.Command{Use: "list", Args: cobra.NoArgs, Short: "List visible project groups.", RunE: func(cmd *cobra.Command, args []string) error {
		_, c, err := g.load()
		if err != nil {
			return err
		}
		r, err := client.Checked(c.ListProjectGroupsWithResponse(cmd.Context()))
		if err != nil {
			return err
		}
		if g.json {
			return render.JSON(cmd.OutOrStdout(), r.JSON200)
		}
		for _, group := range *r.JSON200 {
			fmt.Fprintf(cmd.OutOrStdout(), "%s\t%s\n", group.Id, group.Name)
		}
		return nil
	}})
	for _, create := range []bool{true, false} {
		var description string
		use, short, count := "create NAME", "Create a project group (administrators only).", 1
		if !create {
			use, short, count = "edit ID NAME", "Replace a group's name and description (administrators only).", 2
		}
		command := &cobra.Command{Use: use, Short: short, Args: cobra.ExactArgs(count), RunE: func(cmd *cobra.Command, args []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			name := args[len(args)-1]
			body := api.SaveProjectGroupRequest{Name: &name, Description: &description}
			var group *api.ProjectGroup
			if create {
				r, err := client.Checked(c.CreateProjectGroupWithResponse(cmd.Context(), body))
				if err != nil {
					return err
				}
				group = r.JSON201
			} else {
				id, err := uuid.Parse(args[0])
				if err != nil {
					return &config.UsageError{Message: "ID is a UUID."}
				}
				r, err := client.Checked(c.ChangeProjectGroupWithResponse(cmd.Context(), id, body))
				if err != nil {
					return err
				}
				group = r.JSON200
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), group)
			}
			fmt.Fprintf(cmd.OutOrStdout(), "%s\t%s\n", group.Id, group.Name)
			return nil
		}}
		command.Flags().StringVar(&description, "description", "", "optional description; omitted on edit clears it")
		root.AddCommand(command)
	}
	root.AddCommand(&cobra.Command{Use: "dissolve ID", Short: "Remove a group, keeping all projects (administrators only).", Args: cobra.ExactArgs(1), RunE: func(cmd *cobra.Command, args []string) error {
		id, err := uuid.Parse(args[0])
		if err != nil {
			return &config.UsageError{Message: "ID is a UUID."}
		}
		_, c, err := g.load()
		if err != nil {
			return err
		}
		_, err = client.Checked(c.DeleteProjectGroupWithResponse(cmd.Context(), id))
		if err != nil {
			return err
		}
		if g.json {
			return render.JSON(cmd.OutOrStdout(), map[string]any{"id": id, "dissolved": true})
		}
		fmt.Fprintln(cmd.OutOrStdout(), "Group dissolved; projects are now ungrouped.")
		return nil
	}})
	return root
}
