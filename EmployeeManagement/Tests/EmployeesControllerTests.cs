using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EmployeeManagement.Controllers;
using EmployeeManagement.Data;
using EmployeeManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace EmployeeManagement.Tests
{
    public class EmployeesControllerTests
    {
        [Fact]
        public async Task GetEmployees_CacheHit_ReturnsEmployeesFromCache()
        {
            // Arrange
            var employees = new List<Employee>
            {
                new Employee { Id = 1, Name = "John Doe" },
                new Employee { Id = 2, Name = "Jane Doe" }
            };
            var serializedEmployees = JsonConvert.SerializeObject(employees);
            var cachedEmployees = Encoding.UTF8.GetBytes(serializedEmployees);

            var mockCache = new Mock<IDistributedCache>();
            mockCache.Setup(c => c.GetAsync("employeeList", It.IsAny<CancellationToken>()))
                .ReturnsAsync(cachedEmployees);

            var mockContext = new Mock<ApplicationDbContext>();

            var controller = new EmployeesController(mockContext.Object, mockCache.Object);

            // Act
            var result = await controller.GetEmployees();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedEmployees = Assert.IsAssignableFrom<List<Employee>>(okResult.Value);
            Assert.Equal(2, returnedEmployees.Count);
            Assert.Equal("John Doe", returnedEmployees[0].Name);
            Assert.Equal("Jane Doe", returnedEmployees[1].Name);

            mockContext.Verify(c => c.Employees, Times.Never);
        }

        [Fact]
        public async Task GetEmployees_CacheMiss_ReturnsEmployeesFromDatabaseAndCaches()
        {
            // Arrange
            var employees = new List<Employee>
            {
                new Employee { Id = 1, Name = "John Doe" },
                new Employee { Id = 2, Name = "Jane Doe" }
            };

            var mockCache = new Mock<IDistributedCache>();
            mockCache.Setup(c => c.GetAsync("employeeList", It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[])null);

            var mockSet = new Mock<DbSet<Employee>>();
            mockSet.As<IQueryable<Employee>>().Setup(m => m.Provider).Returns(employees.AsQueryable().Provider);
            mockSet.As<IQueryable<Employee>>().Setup(m => m.Expression).Returns(employees.AsQueryable().Expression);
            mockSet.As<IQueryable<Employee>>().Setup(m => m.ElementType).Returns(employees.AsQueryable().ElementType);
            mockSet.As<IQueryable<Employee>>().Setup(m => m.GetEnumerator()).Returns(employees.AsQueryable().GetEnumerator());

            var mockContext = new Mock<ApplicationDbContext>();
            mockContext.Setup(c => c.Employees).Returns(mockSet.Object);

            var controller = new EmployeesController(mockContext.Object, mockCache.Object);

            // Act
            var result = await controller.GetEmployees();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedEmployees = Assert.IsAssignableFrom<List<Employee>>(okResult.Value);
            Assert.Equal(2, returnedEmployees.Count);
            Assert.Equal("John Doe", returnedEmployees[0].Name);
            Assert.Equal("Jane Doe", returnedEmployees[1].Name);

            mockCache.Verify(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()
            ), Times.Once);
        }

        [Fact]
        public async Task GetEmployee_CacheHit_ReturnsEmployeeFromCache()
        {
            // Arrange
            var employee = new Employee { Id = 1, Name = "John Doe" };
            var serializedEmployee = JsonConvert.SerializeObject(employee);
            var cachedEmployee = Encoding.UTF8.GetBytes(serializedEmployee);

            var mockCache = new Mock<IDistributedCache>();
            mockCache.Setup(c => c.GetAsync("employee_1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(cachedEmployee);

            var mockContext = new Mock<ApplicationDbContext>();

            var controller = new EmployeesController(mockContext.Object, mockCache.Object);

            // Act
            var result = await controller.GetEmployee(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedEmployee = Assert.IsType<Employee>(okResult.Value);
            Assert.Equal(1, returnedEmployee.Id);
            Assert.Equal("John Doe", returnedEmployee.Name);

            mockContext.Verify(c => c.Employees.FindAsync(1), Times.Never);
        }

        [Fact]
        public async Task GetEmployee_CacheMiss_ReturnsEmployeeFromDatabaseAndCaches()
        {
            // Arrange
            var employee = new Employee { Id = 1, Name = "John Doe" };

            var mockCache = new Mock<IDistributedCache>();
            mockCache.Setup(c => c.GetAsync("employee_1", It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[])null);

            var mockContext = new Mock<ApplicationDbContext>();
            mockContext.Setup(c => c.Employees.FindAsync(1))
                .ReturnsAsync(employee);

            var controller = new EmployeesController(mockContext.Object, mockCache.Object);

            // Act
            var result = await controller.GetEmployee(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedEmployee = Assert.IsType<Employee>(okResult.Value);
            Assert.Equal(1, returnedEmployee.Id);
            Assert.Equal("John Doe", returnedEmployee.Name);

            mockCache.Verify(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()
            ), Times.Once);
        }
    }
}