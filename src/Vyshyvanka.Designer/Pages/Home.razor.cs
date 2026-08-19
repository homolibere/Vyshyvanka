namespace Vyshyvanka.Designer.Pages;

public partial class Home
{
    private ManagementSection _activeSection = ManagementSection.Workflows;

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
        Settings
    }
}
