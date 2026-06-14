using Backend_ActiviteitenPlanner;
using Backend_ActiviteitenPlanner.Controllers;
using Backend_ActiviteitenPlanner.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

public class ActivitiesControllerTests
{
    private AppDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // BELANGRIJK FIX
            .Options;

        var context = new AppDbContext(options);

        context.Activities.Add(new Activity
        {
            Title = "Test Activity",
            Description = "Beschrijving",
            Date = "2026-06-14",
            Time = "10:00",
            Location = "Utrecht"
        });

        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task GetAll_ReturnsActivities()
    {
        // Arrange
        var db = GetDbContext();
        var controller = new ActivitiesController(db);

        // Act
        var result = await controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);

        var data = Assert.IsAssignableFrom<IEnumerable<ActivityDto>>(okResult.Value);

        Assert.NotNull(data);
        Assert.True(data.Any());
    }
}