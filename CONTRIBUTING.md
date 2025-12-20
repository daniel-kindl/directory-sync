# Contributing to DirectorySync

Thank you for your interest in contributing to DirectorySync! This document provides guidelines and instructions for contributing.

## Code of Conduct

Be respectful, professional, and constructive in all interactions.

## How to Contribute

### Reporting Bugs

1. Check if the bug has already been reported in [Issues](https://github.com/daniel-kindl/directory-sync/issues)
2. If not, create a new issue using the bug report template
3. Provide detailed information:
   - Steps to reproduce
   - Expected vs actual behavior
   - System information (OS, .NET version)
   - Logs if available

### Suggesting Features

1. Check if the feature has already been requested
2. Create a new issue using the feature request template
3. Clearly describe the feature and its use case

### Pull Requests

1. **Fork and Clone**
   ```bash
   git clone https://github.com/YOUR-USERNAME/directory-sync.git
   cd directory-sync
   ```

2. **Create a Branch**
   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/your-bug-fix
   ```

3. **Make Your Changes**
   - Follow the coding standards below
   - Write or update tests as needed
   - Update documentation if applicable

4. **Test Your Changes**
   ```bash
   dotnet restore
   dotnet build
   dotnet test
   dotnet format --verify-no-changes
   ```

5. **Commit Your Changes**
   Use [Conventional Commits](https://www.conventionalcommits.org/) format:
   ```
   feat: add new feature
   fix: resolve bug
   docs: update documentation
   test: add or update tests
   refactor: code refactoring
   style: formatting changes
   chore: maintenance tasks
   ```

6. **Push and Create PR**
   ```bash
   git push origin feature/your-feature-name
   ```
   Then create a pull request on GitHub

## Development Setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git
- Your favorite IDE (Visual Studio, Rider, or VS Code)

### Building

```bash
dotnet restore
dotnet build --configuration Release
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test Tests/DirectorySync.Tests.csproj

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Testing Locally

```bash
cd Cli
dotnet run -- sync -s ../test-source -r ../test-replica
```

## Coding Standards

### C# Style

- Follow [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use `dotnet format` to ensure consistent formatting
- Maximum line length: 120 characters
- Use meaningful variable and method names
- Add XML documentation for all public APIs

### Project Structure

```
Engine/          - Core synchronization logic (library)
Cli/             - CLI frontend (tool)
Tests/           - Unit tests
.github/         - CI/CD workflows
```

### Testing

- Write unit tests for new features
- Maintain or improve code coverage
- Use xUnit and FluentAssertions
- Follow AAA pattern (Arrange, Act, Assert)

Example:
```csharp
[Fact]
public void ShouldSyncFiles()
{
    // Arrange
    var source = CreateTestDirectory();
    var replica = CreateTestDirectory();
    
    // Act
    var result = SyncEngine.Sync(source, replica);
    
    // Assert
    result.Should().BeSuccessful();
}
```

### Git Workflow

1. Keep commits small and focused
2. Write clear commit messages
3. Squash commits before merging if needed
4. Rebase on main before submitting PR

## Review Process

1. All PRs require at least one approval
2. CI/CD checks must pass
3. Code coverage should not decrease
4. Documentation must be updated if applicable

## Questions?

Feel free to open a discussion or reach out through issues.

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
