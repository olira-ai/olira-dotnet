#nullable enable

namespace Olira;

public sealed partial class OliraClient
{
    /// <summary>
    /// Create a project (isolated workspace). Requires api:manage-projects scope and an org-wide key.
    /// </summary>
    public Project CreateProject(
        string name,
        string? slug = null,
        string? description = null,
        string? environment = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?> { ["name"] = name };
        if (slug is not null)
        {
            body["slug"] = slug;
        }

        if (description is not null)
        {
            body["description"] = description;
        }

        if (environment is not null)
        {
            body["environment"] = environment;
        }

        return _transport.CreateProject(body);
    }

    /// <summary>List the organisation's projects.</summary>
    public ProjectListResult ListProjects()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListProjects();
    }

    /// <summary>Get one project by id or slug.</summary>
    public Project GetProject(string project)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetProject(project);
    }

    /// <summary>Duplicate an existing project's configuration into a new one.</summary>
    public Project DuplicateProject(
        string project,
        string name,
        string? slug = null,
        string? description = null,
        string? environment = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?> { ["name"] = name };
        if (slug is not null)
        {
            body["slug"] = slug;
        }

        if (description is not null)
        {
            body["description"] = description;
        }

        if (environment is not null)
        {
            body["environment"] = environment;
        }

        return _transport.DuplicateProject(project, body);
    }

    /// <summary>Rename a project or update its description/environment tag.</summary>
    public Project RenameProject(
        string project,
        string? name = null,
        string? description = null,
        string? environment = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?>();
        if (name is not null)
        {
            body["name"] = name;
        }

        if (description is not null)
        {
            body["description"] = description;
        }

        if (environment is not null)
        {
            body["environment"] = environment;
        }

        return _transport.UpdateProject(project, body);
    }

    /// <summary>Soft-delete a project (moves it to the deprecated list; data retained).</summary>
    public Project DeprecateProject(string project)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.DeprecateProject(project);
    }

    /// <summary>Reactivate a deprecated project.</summary>
    public Project RestoreProject(string project)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.RestoreProject(project);
    }

    /// <summary>Permanently delete a deprecated project and its config.</summary>
    public void DeleteProject(string project)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _transport.DeleteProject(project);
    }
}
