# Implementation Plan: API Integration Tests

## Overview

This plan implements integration tests for the FlowForge API project. The implementation follows a bottom-up approach: first establishing test infrastructure, then implementing tests for each controller area, and finally adding property-based tests for universal behaviors.

## Tasks

- [x] 1. Set up test infrastructure
  - [x] 1.1 Add required NuGet packages to FlowForge.Tests.csproj
    - Add Microsoft.AspNetCore.Mvc.Testing for WebApplicationFactory
    - _Requirements: 1.1, 1.2_
  - [x] 1.2 Create CustomWebApplicationFactory class
    - Configure in-memory SQLite database
    - Override service registrations for test isolation
    - Configure shorter JWT token expiry for tests
    - _Requirements: 1.1, 1.2, 1.6_
  - [x] 1.3 Create FlowForgeApiFixture class
    - Implement IAsyncLifetime for async setup/teardown
    - Provide AnonymousClient property
    - Implement CreateAuthenticatedClientAsync method
    - Implement CreateApiKeyClientAsync method
    - Seed test users on initialization
    - _Requirements: 1.1, 1.3, 1.4, 1.5_
  - [x] 1.4 Create TestDataFactory helper class
    - Add CreateValidWorkflow method with trigger node
    - Add CreateInvalidWorkflow method without trigger
    - Add CreateExecutionRequest method
    - _Requirements: 3.1, 3.2, 4.1_

- [x] 2. Implement authentication endpoint tests
  - [x] 2.1 Create AuthControllerTests class
    - Test valid login returns 200 with tokens
    - Test invalid password returns 401
    - Test non-existent email returns 401
    - Test empty credentials returns 400
    - _Requirements: 2.1, 2.2, 2.3, 2.8_
  - [x] 2.2 Add registration tests to AuthControllerTests
    - Test valid registration returns 200 with tokens
    - Test duplicate email returns 400
    - _Requirements: 2.4, 2.5_
  - [x] 2.3 Add token refresh tests to AuthControllerTests
    - Test valid refresh token returns new access token
    - Test invalid refresh token returns 401
    - _Requirements: 2.6, 2.7_

- [x] 3. Implement workflow CRUD endpoint tests
  - [x] 3.1 Create WorkflowControllerTests class
    - Test create workflow returns 201 with details
    - Test create invalid workflow returns 400 with validation errors
    - _Requirements: 3.1, 3.2_
  - [x] 3.2 Add workflow retrieval tests
    - Test get by ID returns 200 with workflow
    - Test get non-existent ID returns 404
    - _Requirements: 3.3, 3.4_
  - [x] 3.3 Add workflow update tests
    - Test update returns 200 with incremented version
    - Test update with wrong version returns 409
    - _Requirements: 3.5, 3.6_
  - [x] 3.4 Add workflow deletion tests
    - Test delete returns 204
    - Test delete non-existent returns 404
    - _Requirements: 3.7, 3.8_
  - [x] 3.5 Write property test for pagination bounds
    - **Property 3: Pagination Bounds Respected**
    - **Validates: Requirements 3.9**
  - [x] 3.6 Write property test for search filtering
    - **Property 4: Search Results Match Query**
    - **Validates: Requirements 3.10**

- [x] 4. Implement execution endpoint tests
  - [x] 4.1 Create ExecutionControllerTests class
    - Test trigger execution returns 202 with details
    - Test trigger inactive workflow returns 400
    - Test trigger non-existent workflow returns 404
    - _Requirements: 4.1, 4.2, 4.3_
  - [x] 4.2 Add execution retrieval tests
    - Test get by ID returns 200 with execution
    - Test get non-existent ID returns 404
    - _Requirements: 4.4, 4.5_
  - [x] 4.3 Add execution cancellation tests
    - Test cancel running execution returns 202
    - Test cancel completed execution returns 400
    - _Requirements: 4.8, 4.9_
  - [x] 4.4 Write property test for execution workflow filtering
    - **Property 5: Execution Workflow Filter Consistency**
    - **Validates: Requirements 4.7**

- [x] 5. Implement webhook endpoint tests
  - [x] 5.1 Create WebhookControllerTests class
    - Test POST webhook triggers workflow returns 200
    - Test GET webhook triggers workflow returns 200
    - Test webhook for inactive workflow returns 400
    - Test webhook for non-existent workflow returns 404
    - _Requirements: 5.1, 5.2, 5.3, 5.4_
  - [x] 5.2 Add webhook data passing tests
    - Test JSON body is passed to execution context
    - Test query parameters are passed to execution context
    - _Requirements: 5.5, 5.6_

- [x] 6. Implement authorization tests
  - [x] 6.1 Create AuthorizationTests class
    - Test anonymous client returns 401 on protected endpoints
    - Test viewer role returns 403 on POST /api/workflow
    - Test editor role succeeds on POST /api/workflow
    - _Requirements: 6.1, 6.2, 6.3_
  - [x] 6.2 Add token validation tests
    - Test expired token returns 401
    - Test invalid signature returns 401
    - _Requirements: 6.4, 6.5_
  - [x] 6.3 Write property test for authenticated client authorization
    - **Property 2: Authenticated Client Authorization**
    - **Validates: Requirements 1.3**

- [x] 7. Implement API key authentication tests
  - [x] 7.1 Create ApiKeyAuthenticationTests class
    - Test valid API key authenticates request
    - Test invalid API key returns 401
    - Test expired API key returns 401
    - Test revoked API key returns 401
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

- [x] 8. Implement error response format tests
  - [x] 8.1 Write property test for error response format
    - **Property 6: Error Response Format Consistency**
    - **Validates: Requirements 8.1, 8.2, 8.3**
  - [x] 8.2 Add validation error format test
    - Test validation errors include field-level details
    - _Requirements: 8.4_

- [x] 9. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tasks are required for comprehensive testing
- Each task references specific requirements for traceability
- Test infrastructure (Task 1) must be completed before other tasks
- Tests within each controller area can be implemented in parallel
- Property tests validate universal correctness properties with 100+ iterations
