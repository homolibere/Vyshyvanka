# Implementation Plan: FlowForge

## Overview

This implementation plan breaks down the FlowForge workflow automation platform into incremental coding tasks. The approach prioritizes core functionality first (workflow engine, node system), followed by the visual designer, and finally advanced features (plugins, security).

## Tasks

- [x] 1. Set up project structure and core interfaces
  - Create .NET 10 solution with projects: FlowForge.Core, FlowForge.Engine, FlowForge.Api, FlowForge.Designer, FlowForge.Tests
  - Define core interfaces: IWorkflowEngine, INode, INodeRegistry, IExpressionEvaluator
  - Set up dependency injection and configuration
  - Configure xUnit and CsCheck for testing
  - _Requirements: 1.1, 2.1, 3.1_

- [x] 2. Implement workflow data models and serialization
  - [x] 2.1 Create Workflow, WorkflowNode, Connection, and Position records
    - Implement JSON serialization attributes
    - Add validation annotations
    - _Requirements: 1.1, 1.3_

  - [x] 2.2 Write property test for workflow round-trip serialization
    - **Property 1: Workflow Serialization Round-Trip**
    - **Validates: Requirements 1.1, 1.6, 1.7**

  - [x] 2.3 Implement WorkflowValidator for schema validation
    - Validate required fields, node references, connection validity
    - Return descriptive ValidationError objects
    - _Requirements: 1.4, 1.5_

  - [x] 2.4 Write property test for workflow validation
    - **Property 2: Workflow Schema Validation**
    - **Validates: Requirements 1.4, 1.5**

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement node system foundation
  - [x] 4.1 Create INode, ITriggerNode, and NodeCategory enum
    - Define NodeInput, NodeOutput records
    - Implement base abstract classes
    - _Requirements: 2.1_

  - [x] 4.2 Implement NodeRegistry with registration and lookup
    - Support registration by type and from assembly
    - Store and retrieve NodeDefinition metadata
    - _Requirements: 2.6, 2.7_

  - [x] 4.3 Write property test for node registration and metadata
    - **Property 4: Node Registration and Metadata Completeness**
    - **Validates: Requirements 2.6, 2.7**

  - [x] 4.4 Implement core trigger nodes
    - ManualTriggerNode, WebhookTriggerNode, ScheduleTriggerNode
    - _Requirements: 2.3_

  - [x] 4.5 Implement core logic nodes
    - IfNode, SwitchNode, MergeNode, LoopNode
    - _Requirements: 2.5_

- [x] 5. Implement workflow execution engine
  - [x] 5.1 Create ExecutionContext and INodeOutputStore
    - Implement variable storage and node output tracking
    - Support cancellation tokens
    - _Requirements: 3.1, 3.8_

  - [x] 5.2 Implement WorkflowEngine.ExecuteAsync
    - Build execution graph from workflow definition
    - Execute nodes in topological order
    - _Requirements: 3.1, 3.2_

  - [x] 5.3 Write property test for topological execution order
    - **Property 6: Topological Execution Order**
    - **Validates: Requirements 3.2**

  - [x] 5.4 Implement data flow between nodes
    - Pass output data to downstream nodes via connections
    - Support multiple outputs and inputs
    - _Requirements: 3.3, 5.1_

  - [x] 5.5 Write property test for data flow
    - **Property 7: Data Flow Between Nodes**
    - **Validates: Requirements 3.3, 5.1**

  - [x] 5.6 Implement parallel branch execution
    - Detect independent branches
    - Execute concurrently using Task.WhenAll
    - _Requirements: 3.5_

  - [x] 5.7 Write property test for parallel execution
    - **Property 8: Parallel Branch Execution**
    - **Validates: Requirements 3.5**

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Implement expression evaluator
  - [x] 7.1 Create IExpressionEvaluator interface and implementation
    - Support property access syntax: `{{ nodes.nodeName.output.field }}`
    - Support array indexing and nested properties
    - _Requirements: 5.2, 5.3_

  - [x] 7.2 Implement built-in transformation functions
    - String functions: toUpper, toLower, trim, substring
    - Date functions: format, parse, addDays
    - Math functions: round, floor, ceil, abs
    - _Requirements: 5.6_

  - [x] 7.3 Integrate expression evaluator with execution engine
    - Evaluate expressions in node configurations at runtime
    - Provide clear error messages for evaluation failures
    - _Requirements: 5.4, 5.5_

  - [x] 7.4 Write property test for expression evaluation
    - **Property 12: Expression Evaluation Correctness**
    - **Validates: Requirements 5.2, 5.3, 5.4, 5.5**

- [-] 8. Implement execution persistence
  - [x] 8.1 Create Execution and NodeExecution data models
    - Define ExecutionStatus and ExecutionMode enums
    - _Requirements: 3.6, 7.1_

  - [x] 8.2 Implement IExecutionRepository with EF Core
    - CRUD operations for executions
    - Query by workflow, status, date range
    - _Requirements: 7.1, 7.6_

  - [x] 8.3 Integrate persistence with workflow engine
    - Persist execution start, node completions, and final result
    - Capture error details for failed executions
    - _Requirements: 3.6, 7.4_

  - [x] 8.4 Write property test for execution persistence
    - **Property 9: Execution State Persistence**
    - **Validates: Requirements 3.6, 7.1, 7.4**

  - [x] 8.5 Write property test for execution filtering
    - **Property 19: Execution History Filtering**
    - **Validates: Requirements 7.6**

- [x] 9. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [-] 10. Implement credential management
  - [x] 10.1 Create Credential and DecryptedCredential models
    - Define CredentialType enum
    - _Requirements: 6.2, 6.3_

  - [x] 10.2 Implement ICredentialEncryption with AES-256
    - Encrypt credentials before storage
    - Decrypt only when needed for execution
    - _Requirements: 6.1_

  - [x] 10.3 Implement ICredentialService
    - CRUD operations with encryption
    - Validate credential schema by type
    - _Requirements: 6.2, 6.6_

  - [x] 10.4 Integrate credentials with execution engine
    - Inject decrypted credentials into node execution context
    - Ensure credentials are scoped to node execution
    - _Requirements: 6.4_

  - [x] 10.5 Write property test for credential security
    - **Property 13: Credential Encryption and Non-Exposure**
    - **Validates: Requirements 6.1, 6.5**

  - [x] 10.6 Write property test for credential injection
    - **Property 14: Credential Injection at Runtime**
    - **Validates: Requirements 6.4**

- [x] 11. Implement REST API
  - [x] 11.1 Create WorkflowController with CRUD endpoints
    - GET/POST/PUT/DELETE for workflows
    - Workflow validation on create/update
    - _Requirements: 8.1_

  - [x] 11.2 Create ExecutionController
    - POST to trigger execution
    - GET for execution status and history
    - _Requirements: 8.2, 8.3_

  - [x] 11.3 Implement webhook trigger endpoint
    - Accept HTTP requests and trigger workflows
    - Pass request data to workflow input
    - _Requirements: 8.4_

  - [x] 11.4 Write property test for webhook execution
    - **Property 16: Webhook Trigger Execution**
    - **Validates: Requirements 8.4**

  - [x] 11.5 Implement API error handling middleware
    - Consistent ApiError response format
    - Map exceptions to appropriate HTTP status codes
    - _Requirements: 8.6_

- [x] 12. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [-] 13. Implement authentication and authorization
  - [x] 13.1 Create User and ApiKey models
    - Define UserRole enum
    - _Requirements: 9.1, 9.6_

  - [x] 13.2 Implement JWT authentication
    - Token generation and validation
    - Support for local accounts
    - _Requirements: 9.1, 8.5_

  - [x] 13.3 Implement API key authentication
    - Key generation with configurable scopes
    - Key validation middleware
    - _Requirements: 9.6, 8.5_

  - [x] 13.4 Write property test for API authentication
    - **Property 15: API Authentication Enforcement**
    - **Validates: Requirements 8.5**

  - [x] 13.5 Implement role-based authorization
    - Permission checking for workflow operations
    - Secure error responses without resource leakage
    - _Requirements: 9.2, 9.3, 9.4_

  - [x] 13.6 Write property test for authorization
    - **Property 17: Authorization Enforcement**
    - **Validates: Requirements 9.3, 9.4**

  - [x] 13.7 Implement audit logging
    - Log authentication attempts
    - Log permission-sensitive operations
    - _Requirements: 9.5_

- [x] 14. Implement plugin system
  - [x] 14.1 Create IPluginLoader and PluginInfo
    - Discover plugins from configured directory
    - Load plugin assemblies
    - _Requirements: 10.1_

  - [x] 14.2 Implement plugin validation
    - Verify plugins implement required interfaces
    - Validate node definitions from plugins
    - _Requirements: 10.2_

  - [x] 14.3 Implement plugin isolation with AssemblyLoadContext
    - Isolate plugin execution
    - Handle plugin exceptions gracefully
    - _Requirements: 10.4, 10.5_

  - [x] 14.4 Write property test for plugin isolation
    - **Property 18: Plugin Isolation and Exception Handling**
    - **Validates: Requirements 10.4, 10.5**

  - [x] 14.5 Implement plugin configuration
    - Support environment variables
    - Support configuration files
    - _Requirements: 10.6_

- [x] 15. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 16. Implement Blazor designer foundation
  - [x] 16.1 Create Blazor WebAssembly project structure
    - Set up routing and layout
    - Configure API client
    - _Requirements: 4.1_

  - [x] 16.2 Implement workflow canvas component
    - SVG-based canvas for node placement
    - Pan and zoom support
    - _Requirements: 4.1_

  - [x] 16.3 Implement node palette component
    - Display available nodes by category
    - Support drag-and-drop to canvas
    - _Requirements: 4.2_

  - [x] 16.4 Implement connection drawing
    - Draw connections between node ports
    - Validate port compatibility
    - _Requirements: 4.3_

  - [x] 16.5 Write property test for connection validation
    - **Property 10: Connection Port Compatibility**
    - **Validates: Requirements 4.3**

  - [x] 16.6 Implement node configuration panel
    - Display configuration form for selected node
    - Support expression input fields
    - _Requirements: 4.7_

- [x] 17. Implement designer state management
  - [x] 17.1 Create workflow state service
    - Track canvas state (nodes, connections, selection)
    - Serialize to workflow JSON
    - _Requirements: 4.5_

  - [x] 17.2 Implement undo/redo system
    - Command pattern for canvas operations
    - Undo/redo stack management
    - _Requirements: 4.6_

  - [ ]* 17.3 Write property test for undo/redo
    - **Property 11: Undo/Redo Reversibility**
    - **Validates: Requirements 4.6**

  - [x] 17.4 Implement real-time validation
    - Validate workflow on changes
    - Display validation errors in UI
    - _Requirements: 4.4_

  - [x] 17.5 Implement execution visualization
    - Show execution progress on canvas
    - Display node input/output data
    - _Requirements: 4.8, 7.2_

- [x] 18. Implement core action nodes
  - [x] 18.1 Implement HttpRequestNode
    - Support GET, POST, PUT, DELETE methods
    - Handle headers, body, query parameters
    - _Requirements: 2.4_

  - [x] 18.2 Implement DatabaseQueryNode
    - Support SQL queries with parameters
    - Return results as JSON
    - _Requirements: 2.4_

  - [x] 18.3 Implement EmailSendNode
    - SMTP configuration via credentials
    - Support HTML and plain text
    - _Requirements: 2.4_

  - [x] 18.4 Implement FileOperationsNode
    - Read, write, delete file operations
    - Support binary and text files
    - _Requirements: 2.4_

- [x] 19. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
