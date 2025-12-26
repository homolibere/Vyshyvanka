# Requirements Document

## Introduction

This document defines the requirements for integration tests covering the FlowForge API project. Integration tests verify that API endpoints work correctly end-to-end, including HTTP request/response handling, authentication, authorization, database persistence, and error handling. The tests use WebApplicationFactory for in-memory hosting and TestContainers for database isolation.

## Glossary

- **Integration_Test_Suite**: The collection of xUnit test classes that verify API endpoint behavior end-to-end
- **Test_Server**: An in-memory ASP.NET Core test server created via WebApplicationFactory
- **Test_Database**: An isolated SQLite database instance created per test class for data isolation
- **Authenticated_Client**: An HttpClient configured with a valid JWT Bearer token for authorized requests
- **Anonymous_Client**: An HttpClient without authentication headers for testing public endpoints
- **Test_Fixture**: A shared test context providing Test_Server and Test_Database setup/teardown

## Requirements

### Requirement 1: Test Infrastructure Setup

**User Story:** As a developer, I want a reusable test infrastructure, so that I can write integration tests without duplicating setup code.

#### Acceptance Criteria

1. THE Test_Fixture SHALL provide a configured Test_Server using WebApplicationFactory
2. THE Test_Fixture SHALL create an isolated Test_Database for each test class
3. THE Test_Fixture SHALL provide factory methods to create Authenticated_Client with valid JWT tokens
4. THE Test_Fixture SHALL provide factory methods to create Anonymous_Client without authentication
5. WHEN a test class completes, THE Test_Fixture SHALL dispose of the Test_Database and Test_Server
6. THE Test_Fixture SHALL configure the Test_Server with test-specific settings (e.g., shorter token expiry)

### Requirement 2: Authentication Endpoint Tests

**User Story:** As a developer, I want integration tests for authentication endpoints, so that I can verify login, registration, and token refresh work correctly.

#### Acceptance Criteria

1. WHEN a valid login request is sent to POST /api/auth/login, THE Test_Server SHALL return 200 OK with access token, refresh token, and user details
2. WHEN an invalid password is sent to POST /api/auth/login, THE Test_Server SHALL return 401 Unauthorized
3. WHEN a non-existent email is sent to POST /api/auth/login, THE Test_Server SHALL return 401 Unauthorized
4. WHEN a valid registration request is sent to POST /api/auth/register, THE Test_Server SHALL return 200 OK with access token and user details
5. WHEN a duplicate email is sent to POST /api/auth/register, THE Test_Server SHALL return 400 Bad Request
6. WHEN a valid refresh token is sent to POST /api/auth/refresh, THE Test_Server SHALL return 200 OK with new access token
7. WHEN an invalid refresh token is sent to POST /api/auth/refresh, THE Test_Server SHALL return 401 Unauthorized
8. WHEN empty credentials are sent to POST /api/auth/login, THE Test_Server SHALL return 400 Bad Request

### Requirement 3: Workflow CRUD Endpoint Tests

**User Story:** As a developer, I want integration tests for workflow endpoints, so that I can verify workflow creation, retrieval, update, and deletion work correctly.

#### Acceptance Criteria

1. WHEN a valid workflow is sent to POST /api/workflow with Authenticated_Client, THE Test_Server SHALL return 201 Created with the workflow details
2. WHEN an invalid workflow (missing trigger) is sent to POST /api/workflow, THE Test_Server SHALL return 400 Bad Request with validation errors
3. WHEN GET /api/workflow/{id} is called with a valid ID, THE Test_Server SHALL return 200 OK with the workflow details
4. WHEN GET /api/workflow/{id} is called with a non-existent ID, THE Test_Server SHALL return 404 Not Found
5. WHEN a valid update is sent to PUT /api/workflow/{id}, THE Test_Server SHALL return 200 OK with updated workflow and incremented version
6. WHEN an update with wrong version is sent to PUT /api/workflow/{id}, THE Test_Server SHALL return 409 Conflict
7. WHEN DELETE /api/workflow/{id} is called with a valid ID, THE Test_Server SHALL return 204 No Content
8. WHEN DELETE /api/workflow/{id} is called with a non-existent ID, THE Test_Server SHALL return 404 Not Found
9. WHEN GET /api/workflow is called with pagination parameters, THE Test_Server SHALL return paginated results
10. WHEN GET /api/workflow is called with search parameter, THE Test_Server SHALL return filtered results

### Requirement 4: Execution Endpoint Tests

**User Story:** As a developer, I want integration tests for execution endpoints, so that I can verify workflow execution triggering and history retrieval work correctly.

#### Acceptance Criteria

1. WHEN a valid execution request is sent to POST /api/execution with Authenticated_Client, THE Test_Server SHALL return 202 Accepted with execution details
2. WHEN an execution request for inactive workflow is sent to POST /api/execution, THE Test_Server SHALL return 400 Bad Request
3. WHEN an execution request for non-existent workflow is sent to POST /api/execution, THE Test_Server SHALL return 404 Not Found
4. WHEN GET /api/execution/{id} is called with a valid execution ID, THE Test_Server SHALL return 200 OK with execution details
5. WHEN GET /api/execution/{id} is called with a non-existent ID, THE Test_Server SHALL return 404 Not Found
6. WHEN GET /api/execution is called with filter parameters, THE Test_Server SHALL return filtered execution history
7. WHEN GET /api/execution/workflow/{workflowId} is called, THE Test_Server SHALL return executions for that workflow
8. WHEN POST /api/execution/{id}/cancel is called on a running execution, THE Test_Server SHALL return 202 Accepted
9. WHEN POST /api/execution/{id}/cancel is called on a completed execution, THE Test_Server SHALL return 400 Bad Request

### Requirement 5: Webhook Endpoint Tests

**User Story:** As a developer, I want integration tests for webhook endpoints, so that I can verify external systems can trigger workflows correctly.

#### Acceptance Criteria

1. WHEN a POST request is sent to /api/webhook/{workflowId} with Anonymous_Client, THE Test_Server SHALL trigger the workflow and return 200 OK
2. WHEN a GET request is sent to /api/webhook/{workflowId}, THE Test_Server SHALL trigger the workflow and return 200 OK
3. WHEN a webhook request is sent for inactive workflow, THE Test_Server SHALL return 400 Bad Request
4. WHEN a webhook request is sent for non-existent workflow, THE Test_Server SHALL return 404 Not Found
5. WHEN a webhook request includes JSON body, THE Test_Server SHALL pass the body data to the workflow execution context
6. WHEN a webhook request includes query parameters, THE Test_Server SHALL pass the query data to the workflow execution context

### Requirement 6: Authorization Enforcement Tests

**User Story:** As a developer, I want integration tests for authorization, so that I can verify role-based access control is enforced correctly.

#### Acceptance Criteria

1. WHEN Anonymous_Client calls a protected endpoint, THE Test_Server SHALL return 401 Unauthorized
2. WHEN Authenticated_Client with Viewer role calls POST /api/workflow, THE Test_Server SHALL return 403 Forbidden
3. WHEN Authenticated_Client with Editor role calls POST /api/workflow, THE Test_Server SHALL return success response
4. WHEN Authenticated_Client with expired token calls a protected endpoint, THE Test_Server SHALL return 401 Unauthorized
5. WHEN Authenticated_Client with invalid token signature calls a protected endpoint, THE Test_Server SHALL return 401 Unauthorized

### Requirement 7: API Key Authentication Tests

**User Story:** As a developer, I want integration tests for API key authentication, so that I can verify external integrations can authenticate correctly.

#### Acceptance Criteria

1. WHEN a valid API key is sent in X-API-Key header, THE Test_Server SHALL authenticate the request
2. WHEN an invalid API key is sent in X-API-Key header, THE Test_Server SHALL return 401 Unauthorized
3. WHEN an expired API key is sent in X-API-Key header, THE Test_Server SHALL return 401 Unauthorized
4. WHEN a revoked API key is sent in X-API-Key header, THE Test_Server SHALL return 401 Unauthorized

### Requirement 8: Error Response Format Tests

**User Story:** As a developer, I want integration tests for error responses, so that I can verify consistent error formatting across all endpoints.

#### Acceptance Criteria

1. WHEN any endpoint returns 400 Bad Request, THE Test_Server SHALL include error code and message in response body
2. WHEN any endpoint returns 404 Not Found, THE Test_Server SHALL include error code and message in response body
3. WHEN any endpoint returns 409 Conflict, THE Test_Server SHALL include error code and message in response body
4. WHEN validation fails, THE Test_Server SHALL include field-level error details in response body
