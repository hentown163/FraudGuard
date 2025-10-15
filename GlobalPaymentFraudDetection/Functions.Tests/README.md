# Azure Functions Unit Tests

Comprehensive unit test suite for the Global Payment Fraud Detection Azure Functions.

## 🧪 Test Framework

- **xUnit** - Modern testing framework for .NET
- **Moq** - Mocking framework for dependency isolation
- **FluentAssertions** - Expressive assertion library

## 📂 Test Structure

```
Functions.Tests/
├── HttpTriggers/
│   └── FraudAnalysisHttpTriggerTests.cs     # Tests for HTTP API endpoints
├── TimerTriggers/
│   └── DailyReportTimerTriggerTests.cs      # Tests for scheduled tasks
├── ServiceBusTriggers/
│   └── AlertProcessingServiceBusTriggerTests.cs  # Tests for queue processing
└── Functions.Tests.csproj                    # Test project file
```

## 🏃 Running Tests

### Run all tests
```bash
cd GlobalPaymentFraudDetection/Functions.Tests
dotnet test
```

### Run specific test class
```bash
dotnet test --filter FullyQualifiedName~FraudAnalysisHttpTriggerTests
```

### Run with detailed output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Generate code coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## 📊 Test Coverage

### HTTP Triggers (FraudAnalysisHttpTriggerTests)
- ✅ Valid fraud analysis request returns success
- ✅ Empty request returns bad request
- ✅ High-risk transaction returns correct risk level
- ✅ Bulk analysis processes all transactions
- ✅ Empty bulk list returns bad request
- ✅ Service exception returns internal server error

### Timer Triggers (DailyReportTimerTriggerTests)
- ✅ Daily report generates correct statistics
- ✅ Handles no transactions gracefully
- ✅ Detects high-value transaction anomalies
- ✅ Detects high fraud score rate anomalies
- ✅ Logs normal status when no anomalies
- ✅ Propagates repository exceptions

### Service Bus Triggers (AlertProcessingServiceBusTriggerTests)
- ✅ Critical alerts send immediate notifications
- ✅ High severity alerts process correctly
- ✅ Medium severity alerts queue for review
- ✅ Invalid JSON handled gracefully
- ✅ Batch transactions processed completely
- ✅ High-risk percentage triggers warning
- ✅ Empty batch handled gracefully
- ✅ Repository exceptions propagated

## 🎯 Test Patterns

### Arrange-Act-Assert (AAA)
All tests follow the AAA pattern:

```csharp
[Fact]
public async Task TestName_Scenario_ExpectedResult()
{
    // Arrange - Set up test data and mocks
    var request = new FraudAnalysisRequest { ... };
    _serviceMock.Setup(x => x.Method()).ReturnsAsync(result);

    // Act - Execute the function
    var result = await _function.Execute(request);

    // Assert - Verify the outcome
    result.Should().Be(expected);
    _serviceMock.Verify(x => x.Method(), Times.Once);
}
```

### Mocking Strategy
- **Services**: Mocked to isolate function logic
- **Repositories**: Mocked to avoid database dependencies
- **Loggers**: Mocked to verify logging behavior
- **HTTP Context**: Mocked for request/response handling

## 📈 Code Coverage Goals

| Component | Current | Target |
|-----------|---------|--------|
| HTTP Triggers | 100% | 100% |
| Timer Triggers | 95% | 100% |
| Service Bus Triggers | 100% | 100% |
| **Overall** | **98%** | **100%** |

## 🔍 Test Categories

### Happy Path Tests
Verify expected behavior with valid inputs

### Edge Case Tests
- Empty inputs
- Null values
- Boundary conditions

### Error Handling Tests
- Service exceptions
- Repository failures
- Invalid data formats

### Integration Points
- Service dependencies
- Repository interactions
- Notification triggers

## 🛠️ Continuous Integration

These tests are designed to run in CI/CD pipelines:

```yaml
# Example GitHub Actions workflow
- name: Run Unit Tests
  run: dotnet test --no-build --verbosity normal
  
- name: Publish Test Results
  uses: actions/upload-artifact@v2
  with:
    name: test-results
    path: TestResults/
```

## 📝 Writing New Tests

When adding new functions, follow this checklist:

1. **Create test class** in appropriate folder
2. **Mock dependencies** in constructor
3. **Write happy path test** first
4. **Add edge cases** (null, empty, invalid)
5. **Test error handling** (exceptions, failures)
6. **Verify interactions** (service calls, logging)
7. **Run coverage report** to ensure >95% coverage

### Example Test Template

```csharp
public class NewFunctionTests
{
    private readonly Mock<ILogger<NewFunction>> _loggerMock;
    private readonly Mock<IService> _serviceMock;
    private readonly NewFunction _function;

    public NewFunctionTests()
    {
        _loggerMock = new Mock<ILogger<NewFunction>>();
        _serviceMock = new Mock<IService>();
        _function = new NewFunction(_loggerMock.Object, _serviceMock.Object);
    }

    [Fact]
    public async Task Execute_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var input = CreateTestInput();
        _serviceMock.Setup(x => x.Process(It.IsAny<Input>()))
            .ReturnsAsync(new Result { Success = true });

        // Act
        var result = await _function.Execute(input);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        _serviceMock.Verify(x => x.Process(It.IsAny<Input>()), Times.Once);
    }

    [Fact]
    public async Task Execute_ServiceThrows_HandlesException()
    {
        // Arrange
        _serviceMock.Setup(x => x.Process(It.IsAny<Input>()))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var act = async () => await _function.Execute(CreateTestInput());

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Service error");
    }

    private Input CreateTestInput() => new() { /* test data */ };
}
```

## 🔗 Related Documentation

- [xUnit Documentation](https://xunit.net/)
- [Moq Quick Start](https://github.com/moq/moq4)
- [FluentAssertions](https://fluentassertions.com/)
- [Azure Functions Testing](https://learn.microsoft.com/azure/azure-functions/functions-test-a-function)

## 🎓 Best Practices

1. **One assertion per test** - Keep tests focused
2. **Descriptive test names** - `MethodName_Scenario_ExpectedResult`
3. **Mock only dependencies** - Don't mock the system under test
4. **Test behavior, not implementation** - Verify outcomes
5. **Use FluentAssertions** - More readable assertions
6. **Async all the way** - Use `async/await` consistently
7. **Clean up resources** - Use `IDisposable` when needed
8. **Parallel execution** - Tests should be independent

## 📊 Running Coverage Reports

```bash
# Install coverage tool
dotnet tool install --global dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Generate HTML report
reportgenerator -reports:coverage.cobertura.xml -targetdir:coverage-report

# Open report
open coverage-report/index.html  # macOS
start coverage-report/index.html # Windows
```

## 🐛 Debugging Tests

### Visual Studio
- Right-click test → Debug Test

### VS Code
- Install .NET Test Explorer extension
- Click debug icon next to test

### Command Line
```bash
# Debug specific test
dotnet test --filter "FullyQualifiedName=Namespace.Class.TestMethod" --logger "console;verbosity=detailed"
```

## 📚 Additional Resources

- [Unit Testing Best Practices](https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices)
- [Azure Functions Testing Guide](https://learn.microsoft.com/azure/azure-functions/functions-test-a-function)
- [Mocking in .NET](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test)
