using CsCheck;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Vyshyvanka.Tests.Integration.Designer.Generators;

namespace Vyshyvanka.Tests.Integration.Designer;

/// <summary>
/// Integration tests for ToastService.
/// Tests toast addition, removal, clearing, and event handling.
/// </summary>
public class ToastServiceTests
{
    #region Toast Addition Tests (Task 14.1)

    /// <summary>
    /// Tests that when a success toast is shown, it is added to the collection.
    /// Validates: Requirements 9.1
    /// </summary>
    [Fact]
    public void WhenSuccessToastShownThenToastAddedToCollection()
    {
        // Arrange
        var service = new ToastService();
        var message = "Operation completed successfully";

        // Act
        service.ShowSuccess(message);

        // Assert
        Assert.Single(service.Toasts);
        Assert.Equal(message, service.Toasts[0].Message);
        Assert.Equal(ToastType.Success, service.Toasts[0].Type);
    }

    /// <summary>
    /// Tests that when an error toast is shown, it is added to the collection.
    /// Validates: Requirements 9.1
    /// </summary>
    [Fact]
    public void WhenErrorToastShownThenToastAddedToCollection()
    {
        // Arrange
        var service = new ToastService();
        var message = "An error occurred";

        // Act
        service.ShowError(message);

        // Assert
        Assert.Single(service.Toasts);
        Assert.Equal(message, service.Toasts[0].Message);
        Assert.Equal(ToastType.Error, service.Toasts[0].Type);
    }

    /// <summary>
    /// Tests that when a warning toast is shown, it is added to the collection.
    /// Validates: Requirements 9.1
    /// </summary>
    [Fact]
    public void WhenWarningToastShownThenToastAddedToCollection()
    {
        // Arrange
        var service = new ToastService();
        var message = "Warning: Check your input";

        // Act
        service.ShowWarning(message);

        // Assert
        Assert.Single(service.Toasts);
        Assert.Equal(message, service.Toasts[0].Message);
        Assert.Equal(ToastType.Warning, service.Toasts[0].Type);
    }

    /// <summary>
    /// Tests that when an info toast is shown, it is added to the collection.
    /// Validates: Requirements 9.1
    /// </summary>
    [Fact]
    public void WhenInfoToastShownThenToastAddedToCollection()
    {
        // Arrange
        var service = new ToastService();
        var message = "Information message";

        // Act
        service.ShowInfo(message);

        // Assert
        Assert.Single(service.Toasts);
        Assert.Equal(message, service.Toasts[0].Message);
        Assert.Equal(ToastType.Info, service.Toasts[0].Type);
    }

    /// <summary>
    /// Tests that when a toast is shown, the OnChange event is raised.
    /// Validates: Requirements 9.1
    /// </summary>
    [Fact]
    public void WhenToastShownThenOnChangeEventRaised()
    {
        // Arrange
        var service = new ToastService();
        var eventRaised = false;
        service.OnChange += () => eventRaised = true;

        // Act
        service.ShowSuccess("Test message");

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Tests that when a toast is shown with a title, the title is set correctly.
    /// Validates: Requirements 9.1
    /// </summary>
    [Fact]
    public void WhenToastShownWithTitleThenTitleIsSet()
    {
        // Arrange
        var service = new ToastService();
        var message = "Test message";
        var title = "Test Title";

        // Act
        service.ShowSuccess(message, title);

        // Assert
        Assert.Single(service.Toasts);
        Assert.Equal(title, service.Toasts[0].Title);
    }

    /// <summary>
    /// Tests that when multiple toasts are shown, all are added to the collection.
    /// Validates: Requirements 9.1
    /// </summary>
    [Fact]
    public void WhenMultipleToastsShownThenAllAddedToCollection()
    {
        // Arrange
        var service = new ToastService();

        // Act
        service.ShowSuccess("Success message");
        service.ShowError("Error message");
        service.ShowWarning("Warning message");
        service.ShowInfo("Info message");

        // Assert
        Assert.Equal(4, service.Toasts.Count);
    }

    /// <summary>
    /// Tests that each toast gets a unique ID.
    /// Validates: Requirements 9.1
    /// </summary>
    [Fact]
    public void WhenMultipleToastsShownThenEachHasUniqueId()
    {
        // Arrange
        var service = new ToastService();

        // Act
        service.ShowSuccess("Message 1");
        service.ShowSuccess("Message 2");
        service.ShowSuccess("Message 3");

        // Assert
        var ids = service.Toasts.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    #endregion

    #region Toast Removal Tests (Task 14.1)

    /// <summary>
    /// Tests that when a toast is removed by ID, it is removed from the collection.
    /// Validates: Requirements 9.2
    /// </summary>
    [Fact]
    public void WhenToastRemovedByIdThenToastRemovedFromCollection()
    {
        // Arrange
        var service = new ToastService();
        service.ShowSuccess("Test message");
        var toastId = service.Toasts[0].Id;
        Assert.Single(service.Toasts);

        // Act
        service.Remove(toastId);

        // Assert
        Assert.Empty(service.Toasts);
    }

    /// <summary>
    /// Tests that when a toast is removed, the OnChange event is raised.
    /// Validates: Requirements 9.2
    /// </summary>
    [Fact]
    public void WhenToastRemovedThenOnChangeEventRaised()
    {
        // Arrange
        var service = new ToastService();
        service.ShowSuccess("Test message");
        var toastId = service.Toasts[0].Id;
        var eventRaised = false;
        service.OnChange += () => eventRaised = true;

        // Act
        service.Remove(toastId);

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Tests that when removing a non-existent toast ID, no exception is thrown.
    /// Validates: Requirements 9.2
    /// </summary>
    [Fact]
    public void WhenRemovingNonExistentToastThenNoExceptionThrown()
    {
        // Arrange
        var service = new ToastService();
        service.ShowSuccess("Test message");

        // Act & Assert - should not throw
        var exception = Record.Exception(() => service.Remove("non-existent-id"));
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that when removing a non-existent toast ID, the OnChange event is not raised.
    /// Validates: Requirements 9.2
    /// </summary>
    [Fact]
    public void WhenRemovingNonExistentToastThenOnChangeEventNotRaised()
    {
        // Arrange
        var service = new ToastService();
        service.ShowSuccess("Test message");
        var eventRaised = false;
        service.OnChange += () => eventRaised = true;

        // Act
        service.Remove("non-existent-id");

        // Assert
        Assert.False(eventRaised);
    }

    /// <summary>
    /// Tests that when one toast is removed, other toasts remain in the collection.
    /// Validates: Requirements 9.2
    /// </summary>
    [Fact]
    public void WhenOneToastRemovedThenOtherToastsRemain()
    {
        // Arrange
        var service = new ToastService();
        service.ShowSuccess("Message 1");
        service.ShowError("Message 2");
        service.ShowWarning("Message 3");
        var toastToRemove = service.Toasts[1].Id;

        // Act
        service.Remove(toastToRemove);

        // Assert
        Assert.Equal(2, service.Toasts.Count);
        Assert.DoesNotContain(service.Toasts, t => t.Id == toastToRemove);
    }

    #endregion

    #region Clear All Toasts Tests (Task 14.1)

    /// <summary>
    /// Tests that when Clear is called, all toasts are removed from the collection.
    /// Validates: Requirements 9.3
    /// </summary>
    [Fact]
    public void WhenClearCalledThenAllToastsRemoved()
    {
        // Arrange
        var service = new ToastService();
        service.ShowSuccess("Message 1");
        service.ShowError("Message 2");
        service.ShowWarning("Message 3");
        Assert.Equal(3, service.Toasts.Count);

        // Act
        service.Clear();

        // Assert
        Assert.Empty(service.Toasts);
    }

    /// <summary>
    /// Tests that when Clear is called, the OnChange event is raised.
    /// Validates: Requirements 9.3
    /// </summary>
    [Fact]
    public void WhenClearCalledThenOnChangeEventRaised()
    {
        // Arrange
        var service = new ToastService();
        service.ShowSuccess("Test message");
        var eventRaised = false;
        service.OnChange += () => eventRaised = true;

        // Act
        service.Clear();

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Tests that when Clear is called on empty collection, no exception is thrown.
    /// Validates: Requirements 9.3
    /// </summary>
    [Fact]
    public void WhenClearCalledOnEmptyCollectionThenNoExceptionThrown()
    {
        // Arrange
        var service = new ToastService();
        Assert.Empty(service.Toasts);

        // Act & Assert - should not throw
        var exception = Record.Exception(() => service.Clear());
        Assert.Null(exception);
    }

    #endregion

    #region Default Timeout Tests (Task 14.1)

    /// <summary>
    /// Tests that success toasts have the correct default timeout.
    /// Validates: Requirements 9.4
    /// </summary>
    [Fact]
    public void WhenSuccessToastShownThenDefaultTimeoutIs5000()
    {
        // Arrange
        var service = new ToastService();

        // Act
        service.ShowSuccess("Test message");

        // Assert
        Assert.Equal(5000, service.Toasts[0].DismissTimeout);
    }

    /// <summary>
    /// Tests that error toasts have the correct default timeout (longer).
    /// Validates: Requirements 9.4
    /// </summary>
    [Fact]
    public void WhenErrorToastShownThenDefaultTimeoutIs8000()
    {
        // Arrange
        var service = new ToastService();

        // Act
        service.ShowError("Test message");

        // Assert
        Assert.Equal(8000, service.Toasts[0].DismissTimeout);
    }

    /// <summary>
    /// Tests that warning toasts have the correct default timeout.
    /// Validates: Requirements 9.4
    /// </summary>
    [Fact]
    public void WhenWarningToastShownThenDefaultTimeoutIs6000()
    {
        // Arrange
        var service = new ToastService();

        // Act
        service.ShowWarning("Test message");

        // Assert
        Assert.Equal(6000, service.Toasts[0].DismissTimeout);
    }

    /// <summary>
    /// Tests that info toasts have the correct default timeout.
    /// Validates: Requirements 9.4
    /// </summary>
    [Fact]
    public void WhenInfoToastShownThenDefaultTimeoutIs5000()
    {
        // Arrange
        var service = new ToastService();

        // Act
        service.ShowInfo("Test message");

        // Assert
        Assert.Equal(5000, service.Toasts[0].DismissTimeout);
    }

    /// <summary>
    /// Tests that custom timeout can be specified.
    /// Validates: Requirements 9.4
    /// </summary>
    [Fact]
    public void WhenCustomTimeoutSpecifiedThenTimeoutIsSet()
    {
        // Arrange
        var service = new ToastService();
        var customTimeout = 10000;

        // Act
        service.ShowSuccess("Test message", dismissTimeout: customTimeout);

        // Assert
        Assert.Equal(customTimeout, service.Toasts[0].DismissTimeout);
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Feature: blazor-integration-tests, Property 17: Toast Service Type Handling
    /// For any toast type (Success, Error, Warning, Info), showing a toast SHALL add it 
    /// to the collection with the correct type and appropriate default timeout.
    /// Validates: Requirements 9.1, 9.4
    /// </summary>
    [Fact]
    public void Property17_ToastServiceTypeHandling()
    {
        var testGen = from toastType in Generators.DesignerGenerators.ToastTypeGen
                      from message in Generators.DesignerGenerators.ToastMessageGen
                      from title in Generators.DesignerGenerators.ToastTitleGen
                      select (toastType, message, title);

        testGen.Sample(data =>
        {
            var (toastType, message, title) = data;

            // Arrange
            var service = new ToastService();

            // Act
            service.Show(toastType, message, title);

            // Assert - Toast is added to collection
            Assert.Single(service.Toasts);
            
            var toast = service.Toasts[0];
            
            // Assert - Toast has correct type
            Assert.Equal(toastType, toast.Type);
            
            // Assert - Toast has correct message
            Assert.Equal(message, toast.Message);
            
            // Assert - Toast has correct title
            Assert.Equal(title, toast.Title);
            
            // Assert - Toast has appropriate default timeout (5000ms for Show method)
            Assert.Equal(5000, toast.DismissTimeout);
            
            // Assert - Toast has a non-empty ID
            Assert.False(string.IsNullOrEmpty(toast.Id));
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 17b: Toast Type-Specific Default Timeouts
    /// For any toast type, using the type-specific Show method SHALL create a toast 
    /// with the correct default timeout for that type.
    /// Validates: Requirements 9.1, 9.4
    /// </summary>
    [Fact]
    public void Property17b_ToastTypeSpecificDefaultTimeouts()
    {
        var testGen = from toastType in Generators.DesignerGenerators.ToastTypeGen
                      from message in Generators.DesignerGenerators.ToastMessageGen
                      select (toastType, message);

        testGen.Sample(data =>
        {
            var (toastType, message) = data;

            // Arrange
            var service = new ToastService();
            var expectedTimeout = toastType switch
            {
                ToastType.Success => 5000,
                ToastType.Error => 8000,
                ToastType.Warning => 6000,
                ToastType.Info => 5000,
                _ => 5000
            };

            // Act - Use type-specific method
            switch (toastType)
            {
                case ToastType.Success:
                    service.ShowSuccess(message);
                    break;
                case ToastType.Error:
                    service.ShowError(message);
                    break;
                case ToastType.Warning:
                    service.ShowWarning(message);
                    break;
                case ToastType.Info:
                    service.ShowInfo(message);
                    break;
            }

            // Assert
            Assert.Single(service.Toasts);
            var toast = service.Toasts[0];
            Assert.Equal(toastType, toast.Type);
            Assert.Equal(message, toast.Message);
            Assert.Equal(expectedTimeout, toast.DismissTimeout);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 18: Toast Removal
    /// For any toast in the collection, removing it by ID SHALL result in the toast 
    /// no longer being in the collection.
    /// Validates: Requirements 9.2, 9.3
    /// </summary>
    [Fact]
    public void Property18_ToastRemoval()
    {
        var testGen = from toastType in DesignerGenerators.ToastTypeGen
                      from message in DesignerGenerators.ToastMessageGen
                      from title in DesignerGenerators.ToastTitleGen
                      select (toastType, message, title);

        testGen.Sample(data =>
        {
            var (toastType, message, title) = data;

            // Arrange
            var service = new ToastService();
            service.Show(toastType, message, title);
            Assert.Single(service.Toasts);
            var toastId = service.Toasts[0].Id;

            // Act
            service.Remove(toastId);

            // Assert - Toast is no longer in collection
            Assert.Empty(service.Toasts);
            Assert.DoesNotContain(service.Toasts, t => t.Id == toastId);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 18b: Toast Removal Preserves Other Toasts
    /// For any collection of toasts, removing one toast by ID SHALL preserve all other toasts.
    /// Validates: Requirements 9.2, 9.3
    /// </summary>
    [Fact]
    public void Property18b_ToastRemovalPreservesOtherToasts()
    {
        var testGen = from messages in DesignerGenerators.ToastMessageGen.List[2, 5]
                      from indexToRemove in Gen.Int[0, 4].Where(i => i < messages.Count)
                      select (messages, indexToRemove);

        testGen.Sample(data =>
        {
            var (messages, indexToRemove) = data;

            // Arrange
            var service = new ToastService();
            foreach (var message in messages)
            {
                service.ShowInfo(message);
            }
            
            var initialCount = service.Toasts.Count;
            var toastToRemove = service.Toasts[indexToRemove];
            var toastIdToRemove = toastToRemove.Id;
            var otherToastIds = service.Toasts
                .Where(t => t.Id != toastIdToRemove)
                .Select(t => t.Id)
                .ToList();

            // Act
            service.Remove(toastIdToRemove);

            // Assert - Removed toast is gone
            Assert.DoesNotContain(service.Toasts, t => t.Id == toastIdToRemove);
            
            // Assert - Other toasts are preserved
            Assert.Equal(initialCount - 1, service.Toasts.Count);
            foreach (var otherId in otherToastIds)
            {
                Assert.Contains(service.Toasts, t => t.Id == otherId);
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 18c: Clear Removes All Toasts
    /// For any collection of toasts, calling Clear SHALL remove all toasts from the collection.
    /// Validates: Requirements 9.3
    /// </summary>
    [Fact]
    public void Property18c_ClearRemovesAllToasts()
    {
        var testGen = from messages in DesignerGenerators.ToastMessageGen.List[1, 10]
                      select messages;

        testGen.Sample(messages =>
        {
            // Arrange
            var service = new ToastService();
            foreach (var message in messages)
            {
                service.ShowInfo(message);
            }
            Assert.Equal(messages.Count, service.Toasts.Count);

            // Act
            service.Clear();

            // Assert - All toasts are removed
            Assert.Empty(service.Toasts);
        }, iter: 100);
    }

    #endregion
}
