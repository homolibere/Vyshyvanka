using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Pages;

public partial class Home
{
    [SupplyParameterFromQuery(Name = "section")]
    private string? Section { get; set; }

    private ManagementSection _activeSection = ManagementSection.Workflows;

    protected override void OnParametersSet()
    {
        if (!string.IsNullOrEmpty(Section) &&
            Enum.TryParse<ManagementSection>(Section, ignoreCase: true, out var parsed))
        {
            _activeSection = parsed;
        }
    }

    private void SetActiveSection(ManagementSection section)
    {
        _activeSection = section;
    }

    private enum ManagementSection
    {
        Workflows,
        Executions,
        Credentials,
        ApiKeys,
        Packages,
        Teams,
        Users,
        Settings
    }
}
