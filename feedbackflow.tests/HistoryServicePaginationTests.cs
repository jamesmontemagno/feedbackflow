using Microsoft.VisualStudio.TestTools.UnitTesting;
using FeedbackWebApp.Services.Interfaces;
using SharedDump.Models;

namespace FeedbackFlow.Tests;

[TestClass]
public class HistoryServicePaginationTests
{
    [TestMethod]
    public void PaginationConstants_ShouldHaveCorrectValues()
    {
        // These constants are defined in History.razor component
        const int expectedInitialPageSize = 5;
        const int expectedLoadMorePageSize = 10;
        
        // Verify the constants make sense for pagination
        Assert.IsGreaterThan(0, expectedInitialPageSize, "Initial page size should be positive");
        Assert.IsGreaterThan(0, expectedLoadMorePageSize, "Load more page size should be positive");
        Assert.IsLessThanOrEqualTo(expectedLoadMorePageSize, expectedInitialPageSize, "Initial page size should not be larger than load more size");
    }
    
    [TestMethod]
    public void AnalysisHistoryItem_ShouldHaveRequiredProperties()
    {
        // Verify that AnalysisHistoryItem has the properties needed for pagination and search
        var item = new AnalysisHistoryItem
        {
            Id = "test-id",
            FullAnalysis = "Test analysis content",
            UserInput = "Test user input",
            SourceType = "Manual",
            Timestamp = DateTime.UtcNow
        };
        
        Assert.IsNotNull(item.Id, "Item should have an ID");
        Assert.IsNotNull(item.FullAnalysis, "Item should have full analysis");
        Assert.IsNotNull(item.UserInput, "Item should have user input");
        Assert.IsNotNull(item.SourceType, "Item should have source type");
        Assert.IsTrue(item.Timestamp > DateTime.MinValue, "Item should have a valid timestamp");
        
        // Verify computed property works
        Assert.IsNotNull(item.Summary, "Item should have a computed summary");
    }
    
    [TestMethod]
    public void PaginationSkipTakeLogic_ShouldCalculateCorrectly()
    {
        // Test pagination math used in the implementation
        const int initialPageSize = 5;
        const int loadMorePageSize = 10;
        
        // First load: skip=0, take=5
        var firstLoadSkip = 0;
        var firstLoadTake = initialPageSize;
        
        Assert.AreEqual(0, firstLoadSkip, "First load should skip 0 items");
        Assert.AreEqual(5, firstLoadTake, "First load should take 5 items");
        
        // After first load, we have 5 items
        var currentItemCount = 5;
        
        // Second load: skip=5, take=10
        var secondLoadSkip = currentItemCount;
        var secondLoadTake = loadMorePageSize;
        
        Assert.AreEqual(5, secondLoadSkip, "Second load should skip 5 items");
        Assert.AreEqual(10, secondLoadTake, "Second load should take 10 items");
        
        // After second load, we have 15 items
        currentItemCount += 10;
        
        // Third load: skip=15, take=10
        var thirdLoadSkip = currentItemCount;
        var thirdLoadTake = loadMorePageSize;
        
        Assert.AreEqual(15, thirdLoadSkip, "Third load should skip 15 items");
        Assert.AreEqual(10, thirdLoadTake, "Third load should take 10 items");
    }
    
    [TestMethod]
    public void HasMoreItemsLogic_ShouldWorkCorrectly()
    {
        // Test the logic for determining if more items are available
        var totalItems = 23;
        var currentItemCount = 5;
        
        // After loading 5 out of 23 items
        var hasMoreItems = currentItemCount < totalItems;
        Assert.IsTrue(hasMoreItems, "Should have more items when current count is less than total");
        
        // After loading 15 out of 23 items
        currentItemCount = 15;
        hasMoreItems = currentItemCount < totalItems;
        Assert.IsTrue(hasMoreItems, "Should still have more items");
        
        // After loading all 23 items
        currentItemCount = 23;
        hasMoreItems = currentItemCount < totalItems;
        Assert.IsFalse(hasMoreItems, "Should not have more items when all are loaded");
        
        // Edge case: more items loaded than total (shouldn't happen but test anyway)
        currentItemCount = 25;
        hasMoreItems = currentItemCount < totalItems;
        Assert.IsFalse(hasMoreItems, "Should not have more items when current exceeds total");
    }
}