# Implementation Plan: NuGet Plugin System

## Overview

This plan implements the NuGet Plugin System for FlowForge, enabling runtime downloading and loading of plugin packages from NuGet feeds. The implementation builds on the existing plugin infrastructure and uses NuGet.Protocol for native NuGet integration.

## Tasks

- [x] 1. Add NuGet package dependencies and create core interfaces
  - Add NuGet.Protocol, NuGet.Packaging, NuGet.Versioning to FlowForge.Engine.csproj
  - Create INuGetPackageManager interface in FlowForge.Core/Interfaces
  - Create IPackageSourceService interface in FlowForge.Core/Interfaces
  - Create IDependencyResolver interface in FlowForge.Core/Interfaces
  - Create IPackageCache interface in FlowForge.Core/Interfaces
  - Create IManifestManager interface in FlowForge.Core/Interfaces
  - _Requirements: 1.1, 2.1, 5.1, 7.1, 9.1_

- [x] 2. Implement data models
  - [x] 2.1 Create package source and credential models
    - Create PackageSource record with Name, Url, IsEnabled, IsTrusted, Credentials, Priority
    - Create PackageSourceCredentials record with Username, Password, ApiKey
    - Create PackageSourceConfig for adding/updating sources
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 2.2 Create installed package and manifest models
    - Create InstalledPackage record with PackageId, Version, SourceName, InstallPath, InstalledAt, NodeTypes, Dependencies, IsLoaded
    - Create PackageManifest record with Version, LastModified, Packages, Sources
    - _Requirements: 9.1, 9.3_

  - [ ]* 2.3 Write property test for PackageManifest round-trip serialization
    - **Property 1: Package Manifest Round-Trip**
    - **Validates: Requirements 9.4**

  - [x] 2.4 Create search and installation result models
    - Create PackageSearchResult, PackageSearchItem records
    - Create PackageInstallResult, PackageUpdateResult, PackageUninstallResult records
    - Create DependencyResolutionResult, PackageDependency, DependencyConflict records
    - _Requirements: 1.2, 2.1, 7.3_

- [x] 3. Implement ManifestManager
  - [x] 3.1 Create ManifestManager class
    - Implement LoadAsync to read manifest from JSON file
    - Implement SaveAsync to write manifest to JSON file
    - Implement AddPackageAsync, RemovePackageAsync, UpdatePackageAsync
    - Handle file locking for concurrent access
    - _Requirements: 9.1, 9.2, 9.3_

  - [x] 3.2 Implement manifest corruption recovery
    - Detect corrupted JSON and attempt recovery from Package_Cache
    - Scan cache directory for installed packages
    - Rebuild manifest from discovered packages
    - _Requirements: 9.5_

  - [ ]* 3.3 Write property test for manifest persistence
    - **Property 9: Offline Loading** (manifest portion)
    - **Validates: Requirements 9.1, 9.2, 9.3**

- [x] 4. Implement PackageCache
  - [x] 4.1 Create PackageCache class
    - Implement GetPackagePathAsync to download packages to cache
    - Implement ExtractPackageAsync to extract nupkg contents
    - Implement RemovePackageAsync to delete cached packages
    - Implement GetExtractionPath for consistent path generation
    - _Requirements: 2.2, 8.1_

  - [x] 4.2 Implement cache cleanup
    - Implement CleanupAsync to remove orphaned cache entries
    - Compare cache contents against manifest
    - Remove packages not in manifest
    - _Requirements: 7.5_

  - [ ]* 4.3 Write property test for orphaned dependency cleanup
    - **Property 7: Orphaned Dependency Cleanup**
    - **Validates: Requirements 7.5**

- [x] 5. Implement PackageSourceService
  - [x] 5.1 Create PackageSourceService class
    - Implement GetSources to return configured sources
    - Implement AddSourceAsync with URL validation
    - Implement RemoveSourceAsync and UpdateSourceAsync
    - Implement TestSourceAsync to verify connectivity
    - Implement GetRepository to create NuGet SourceRepository
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 5.2 Implement source persistence
    - Save sources to manifest on add/update/remove
    - Load sources from manifest on startup
    - Support authenticated sources with encrypted credentials
    - _Requirements: 5.6_

  - [ ]* 5.3 Write property test for source persistence
    - **Property 10: Package Source Persistence**
    - **Validates: Requirements 5.1, 5.2, 5.6**

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Implement DependencyResolver
  - [x] 7.1 Create DependencyResolver class
    - Implement ResolveAsync using NuGet.Protocol dependency resolution
    - Build dependency graph from package metadata
    - Select compatible versions for shared dependencies
    - _Requirements: 7.1, 7.2_

  - [x] 7.2 Implement conflict detection
    - Detect version conflicts with installed packages
    - Return detailed conflict information
    - Implement CheckUpdateCompatibilityAsync
    - _Requirements: 2.5, 7.3_

  - [x] 7.3 Write property test for dependency conflict detection
    - **Property 5: Dependency Conflict Detection**
    - **Validates: Requirements 2.5, 7.3**

  - [x] 7.4 Write property test for dependency resolution completeness
    - **Property 6: Dependency Resolution Completeness**
    - **Validates: Requirements 7.1, 7.2**

- [x] 8. Implement NuGetPackageManager - Search
  - [x] 8.1 Implement SearchPackagesAsync
    - Query all enabled sources using NuGet.Protocol SearchAsync
    - Aggregate results from multiple sources
    - Enrich results with installation status from manifest
    - Handle source failures gracefully
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [ ]* 8.2 Write property test for search results completeness
    - **Property 2: Search Results Completeness**
    - **Validates: Requirements 1.1, 1.2, 1.3**

  - [ ]* 8.3 Write property test for source unavailability resilience
    - **Property 8: Source Unavailability Resilience**
    - **Validates: Requirements 1.5, 8.4**

- [x] 9. Implement NuGetPackageManager - Installation
  - [x] 9.1 Implement InstallPackageAsync
    - Download package using PackageCache
    - Resolve dependencies using DependencyResolver
    - Verify package signature (if required)
    - Extract package contents
    - Validate plugin interfaces
    - Update manifest
    - Load plugin using existing PluginLoader
    - Register nodes with NodeRegistry
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.6, 2.7_

  - [x] 9.2 Implement security validation
    - Verify NuGet package signatures
    - Check allow/block lists
    - Validate INode interface implementation
    - Require confirmation for untrusted sources
    - _Requirements: 6.1, 6.2, 6.3, 6.6_

  - [x] 9.3 Write property test for installation completeness
    - **Property 3: Installation Completeness**
    - **Validates: Requirements 2.1, 2.2, 2.4, 2.6, 2.7**

  - [x] 9.4 Write property test for plugin interface validation
    - **Property 12: Plugin Interface Validation**
    - **Validates: Requirements 6.3**

  - [x] 9.5 Write property test for allow/block list enforcement
    - **Property 13: Allow/Block List Enforcement**
    - **Validates: Requirements 6.6**

  - [x] 9.6 Write property test for untrusted source confirmation
    - **Property 11: Untrusted Source Confirmation**
    - **Validates: Requirements 5.5**

- [x] 10. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Implement NuGetPackageManager - Updates
  - [x] 11.1 Implement CheckForUpdatesAsync
    - Query sources for newer versions of installed packages
    - Return update information with version and changelog
    - _Requirements: 3.1, 3.2_

  - [x] 11.2 Implement UpdatePackageAsync
    - Check compatibility with existing workflows
    - Download and install new version
    - Unload old version, load new version
    - Update manifest
    - _Requirements: 3.3, 3.4, 3.5, 3.6_

  - [ ]* 11.3 Write property test for update version ordering
    - **Property 14: Update Version Ordering**
    - **Validates: Requirements 3.1, 3.2**

- [x] 12. Implement NuGetPackageManager - Uninstallation
  - [x] 12.1 Implement UninstallPackageAsync
    - Check for workflow references
    - Unload plugin assemblies
    - Remove package files from cache
    - Remove orphaned dependencies
    - Update manifest
    - Unregister nodes from NodeRegistry
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

  - [x] 12.2 Write property test for uninstallation completeness
    - **Property 4: Uninstallation Completeness**
    - **Validates: Requirements 4.3, 4.4, 4.5, 4.6**

  - [x] 12.3 Write property test for workflow reference detection
    - **Property 15: Workflow Reference Detection**
    - **Validates: Requirements 4.1, 4.2**

- [x] 13. Implement NuGetPackageManager - Initialization
  - [x] 13.1 Implement InitializeAsync
    - Load manifest from disk
    - Load all packages from cache without network
    - Register nodes with NodeRegistry
    - Handle missing packages gracefully
    - _Requirements: 8.2, 9.2_

  - [x] 13.2 Write property test for offline loading
    - **Property 9: Offline Loading**
    - **Validates: Requirements 8.1, 8.2**

- [x] 14. Create REST API endpoints
  - [x] 14.1 Create PackageController
    - GET /api/packages/search - Search for packages
    - GET /api/packages - List installed packages
    - GET /api/packages/{id} - Get package details
    - POST /api/packages/{id}/install - Install package
    - POST /api/packages/{id}/update - Update package
    - DELETE /api/packages/{id} - Uninstall package
    - GET /api/packages/updates - Check for updates
    - _Requirements: 1.1, 2.1, 3.1, 4.1_

  - [x] 14.2 Create PackageSourceController
    - GET /api/packages/sources - List sources
    - POST /api/packages/sources - Add source
    - PUT /api/packages/sources/{name} - Update source
    - DELETE /api/packages/sources/{name} - Remove source
    - POST /api/packages/sources/{name}/test - Test connectivity
    - _Requirements: 5.1, 5.2_

- [x] 15. Register services and configuration
  - [x] 15.1 Add service registration
    - Register INuGetPackageManager, IPackageSourceService, IDependencyResolver, IPackageCache, IManifestManager
    - Add configuration binding for package settings
    - Call InitializeAsync on application startup
    - _Requirements: 8.2, 9.2_

  - [x] 15.2 Add configuration schema
    - Add PackageOptions configuration class
    - Configure default nuget.org source
    - Support environment variable overrides
    - _Requirements: 5.1_

- [x] 16. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
- The implementation uses NuGet.Protocol for native NuGet feed integration
- Assembly loading uses the existing PluginLoadContext for isolation
