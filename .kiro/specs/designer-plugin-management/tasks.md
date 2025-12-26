# Implementation Plan: Designer Plugin Management

## Overview

This plan implements the Plugin Manager UI for the FlowForge Designer application. The implementation adds Blazor components for browsing, installing, updating, and uninstalling NuGet packages, along with a service layer for state management and API communication.

## Tasks

- [x] 1. Create plugin data models
  - [x] 1.1 Create PluginModels.cs with all data models
    - Create InstalledPackageModel, PackageSearchItemModel, PackageSearchResultModel
    - Create PackageDetailsModel, PackageInstallResultModel, PackageUpdateResultModel
    - Create PackageUninstallResultModel, PackageUpdateInfoModel
    - Create PackageSourceModel, SourceTestResultModel
    - _Requirements: 2.1, 3.1, 7.1, 8.1_

- [x] 2. Extend FlowForgeApiClient with package operations
  - [x] 2.1 Add package management methods to FlowForgeApiClient
    - Add GetInstalledPackagesAsync method
    - Add SearchPackagesAsync method with pagination
    - Add GetPackageDetailsAsync method
    - Add InstallPackageAsync method
    - Add UpdatePackageAsync method
    - Add UninstallPackageAsync method
    - Add CheckForUpdatesAsync method
    - _Requirements: 2.1, 3.2, 4.1, 5.1, 6.3, 8.1_

  - [x] 2.2 Add package source methods to FlowForgeApiClient
    - Add GetPackageSourcesAsync method
    - Add AddPackageSourceAsync method
    - Add UpdatePackageSourceAsync method
    - Add RemovePackageSourceAsync method
    - Add TestPackageSourceAsync method
    - _Requirements: 7.1, 7.3, 7.5, 7.6_

- [x] 3. Create PluginStateService
  - [x] 3.1 Implement PluginStateService core functionality
    - Create service with state properties (InstalledPackages, SearchResults, Sources, etc.)
    - Implement OnStateChanged event for component updates
    - Implement LoadInstalledPackagesAsync
    - Implement SearchPackagesAsync with debouncing
    - Implement GetPackageDetailsAsync
    - _Requirements: 1.4, 2.1, 3.2, 8.1_

  - [x] 3.2 Implement package operation methods
    - Implement InstallPackageAsync with progress tracking
    - Implement UpdatePackageAsync with progress tracking
    - Implement UninstallPackageAsync with workflow reference check
    - Implement CheckForUpdatesAsync
    - Integrate with WorkflowStateService for node refresh
    - _Requirements: 4.1, 4.4, 5.1, 5.4, 6.3, 6.6_

  - [x] 3.3 Implement source management methods
    - Implement LoadSourcesAsync
    - Implement AddSourceAsync with validation
    - Implement UpdateSourceAsync
    - Implement RemoveSourceAsync
    - Implement TestSourceAsync
    - _Requirements: 7.1, 7.3, 7.5, 7.6, 7.7_

- [x] 4. Checkpoint - Ensure services compile and basic tests pass
  - Ensure all services compile, ask the user if questions arise.

- [x] 5. Create PackageCard component
  - [x] 5.1 Implement PackageCard.razor
    - Create component with parameters for package data
    - Display package icon, name, version, description
    - Display author, download count, node count
    - Implement conditional Install/Update/Uninstall buttons
    - Display update indicator when HasUpdate is true
    - Add click handler for opening details
    - _Requirements: 2.2, 2.3, 2.4, 3.3, 3.4, 3.5_

  - [x] 5.2 Write property test for PackageCard information completeness
    - **Property 1: Installed Package Card Information Completeness**
    - **Property 2: Search Result Card Information Completeness**
    - **Validates: Requirements 2.2, 2.3, 2.4, 3.3, 3.4, 3.5**

- [x] 6. Create InstalledPackages component
  - [x] 6.1 Implement InstalledPackages.razor
    - Create tab content component
    - Display list of installed packages using PackageCard
    - Implement "Check for Updates" button
    - Display empty state when no packages installed
    - Handle loading state
    - _Requirements: 2.1, 2.5, 5.6_

- [x] 7. Create BrowsePackages component
  - [x] 7.1 Implement BrowsePackages.razor
    - Create tab content component with search input
    - Implement search with debounce
    - Display search results using PackageCard
    - Display loading state during search
    - Display "no results" message when empty
    - Display error message with retry on API failure
    - _Requirements: 3.1, 3.2, 3.6, 3.7, 3.8_

  - [x] 7.2 Write property test for search results display
    - **Property 9: Search Results Display All Matching Packages**
    - **Validates: Requirements 3.2**

- [x] 8. Create SourceManager component
  - [x] 8.1 Implement SourceManager.razor
    - Create tab content component
    - Display list of configured sources
    - Show source name, URL, enabled/trusted status
    - Implement Add Source button
    - Implement Edit, Remove, Test actions per source
    - Display test results (success/failure with response time)
    - _Requirements: 7.1, 7.2, 7.3, 7.5, 7.6, 7.7_

  - [x] 8.2 Write property test for source list completeness
    - **Property 4: Source List Information Completeness**
    - **Validates: Requirements 7.1, 7.2, 7.5**

- [x] 9. Create SourceEditModal component
  - [x] 9.1 Implement SourceEditModal.razor
    - Create modal for adding/editing sources
    - Include form fields: name, URL, enabled, trusted
    - Include optional credential fields: username, password, API key
    - Implement form validation
    - _Requirements: 7.3, 7.4_

- [x] 10. Checkpoint - Ensure tab components render correctly
  - Ensure all tab components compile and render, ask the user if questions arise.

- [x] 11. Create PackageDetailsModal component
  - [x] 11.1 Implement PackageDetailsModal.razor
    - Create modal for detailed package view
    - Display package icon, title, version, author
    - Display description, license, project URL, tags
    - Display version selector with all available versions
    - Display dependencies list
    - Display node types list (if installed)
    - Implement Install/Update/Uninstall actions based on state
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6_

  - [x] 11.2 Write property test for package details completeness
    - **Property 5: Package Details Information Completeness**
    - **Validates: Requirements 8.2, 8.3, 8.4, 8.5, 8.6**

- [x] 12. Create supporting UI components
  - [x] 12.1 Implement ConfirmDialog.razor
    - Create reusable confirmation dialog
    - Support custom title, message, and button text
    - Support displaying list of affected items (for workflow warnings)
    - _Requirements: 6.1, 6.2_

  - [x] 12.2 Implement Toast.razor
    - Create toast notification component
    - Support success, error, warning, info types
    - Auto-dismiss after timeout
    - _Requirements: 10.1, 10.2_

  - [x] 12.3 Write property test for workflow reference warning
    - **Property 7: Workflow Reference Warning on Uninstall**
    - **Validates: Requirements 6.2**

- [x] 13. Create PluginManager main component
  - [x] 13.1 Implement PluginManager.razor
    - Create main container component (modal or side panel)
    - Implement tab navigation (Installed, Browse, Sources)
    - Wire up child components
    - Handle open/close state
    - Load initial data on open
    - _Requirements: 1.2, 1.3, 1.4_

  - [x] 13.2 Add Plugin Manager button to Designer
    - Add button to Designer toolbar or header
    - Wire up click handler to open PluginManager
    - _Requirements: 1.1_

- [x] 14. Implement package operation flows
  - [x] 14.1 Implement installation flow
    - Handle Install button click
    - Show confirmation for untrusted sources
    - Display progress during installation
    - Update UI on success
    - Trigger node refresh
    - Display error on failure with retry
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

  - [x] 14.2 Write property test for untrusted source confirmation
    - **Property 8: Untrusted Source Confirmation**
    - **Validates: Requirements 4.6**

  - [x] 14.3 Implement update flow
    - Handle Update button click
    - Display progress during update
    - Update UI on success with new version
    - Trigger node refresh
    - Display error on failure with retry
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 14.4 Implement uninstallation flow
    - Handle Uninstall button click
    - Show confirmation dialog
    - Display workflow warnings if applicable
    - Display progress during uninstallation
    - Remove package from list on success
    - Trigger node refresh
    - Display error on failure
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

- [x] 15. Checkpoint - Ensure all operations work end-to-end
  - Ensure all package operations work, ask the user if questions arise.

- [x] 16. Implement Node Palette integration
  - [x] 16.1 Update WorkflowStateService for plugin node refresh
    - Ensure SetNodeDefinitions properly updates palette
    - Verify nodes are grouped by category
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [x] 16.2 Write property test for node refresh after operations
    - **Property 3: Node Refresh After Package Operations**
    - **Validates: Requirements 4.4, 5.4, 6.6, 9.1, 9.2, 9.3**

  - [x] 16.3 Write property test for node palette grouping
    - **Property 10: Node Palette Grouping**
    - **Validates: Requirements 9.4**

- [x] 17. Implement UI state management
  - [x] 17.1 Add loading states and button disabling
    - Disable action buttons during operations
    - Show loading indicators for all async operations
    - Prevent duplicate requests
    - _Requirements: 10.4, 10.5_

  - [x] 17.2 Write property test for UI state management
    - **Property 6: UI State Management During Operations**
    - **Validates: Requirements 10.4, 10.5**

- [x] 18. Add error handling and feedback
  - [x] 18.1 Implement error handling
    - Display connection errors with retry
    - Display operation errors with details
    - Handle API unreachable scenarios
    - _Requirements: 10.2, 10.3_

  - [x] 18.2 Implement success notifications
    - Show toast on successful install
    - Show toast on successful update
    - Show toast on successful uninstall
    - _Requirements: 10.1_

- [x] 19. Add CSS styling
  - [x] 19.1 Create component styles
    - Create PluginManager.razor.css
    - Create PackageCard.razor.css
    - Create PackageDetailsModal.razor.css
    - Style tabs, lists, cards, modals, buttons
    - Ensure consistent look with existing Designer UI

- [x] 20. Register services
  - [x] 20.1 Register PluginStateService in Program.cs
    - Add PluginStateService as scoped service
    - Ensure proper dependency injection

- [x] 21. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tasks are required for comprehensive implementation
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
- The implementation uses existing FlowForgeApiClient patterns
- CSS styling follows existing Designer conventions

