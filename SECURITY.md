# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

## Security Features

DirectorySync includes multiple security features:

- **Path Traversal Protection**: All file paths are validated to prevent directory traversal attacks
- **Symbolic Link Rejection**: Symbolic links are automatically detected and skipped
- **Path Validation**: Prevents dangerous operations (source == replica, nested paths)
- **Input Sanitization**: All user inputs are validated and sanitized
- **No Remote Code Execution**: Pure file system operations only

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report them privately via one of these methods:

1. **GitHub Security Advisories** (Preferred)
   - Go to https://github.com/daniel-kindl/directory-sync/security/advisories
   - Click "Report a vulnerability"
   - Fill out the form with details

2. **Email**
   - Send to: daniel.kindl@example.com
   - Subject: "[SECURITY] DirectorySync vulnerability report"
   - Include:
     - Description of the vulnerability
     - Steps to reproduce
     - Potential impact
     - Suggested fix (if any)

## Response Timeline

- **Initial Response**: Within 48 hours
- **Status Update**: Within 1 week
- **Fix Timeline**: Depends on severity
  - Critical: 1-3 days
  - High: 1-2 weeks
  - Medium: 2-4 weeks
  - Low: Next release

## Disclosure Policy

- Security issues will be kept confidential until a fix is released
- We will credit reporters in the release notes (unless you prefer to remain anonymous)
- We will publish a security advisory after the fix is released

## Security Update Process

1. Vulnerability reported and confirmed
2. Fix developed and tested
3. Security advisory created (but not published)
4. New version released with fix
5. Security advisory published
6. Users notified via GitHub releases

## Best Practices for Users

### Safe Usage

1. **Verify Paths**: Always double-check source and replica paths before syncing
2. **Use Plan Command**: Use `dirsync plan` to preview changes before executing
3. **Start with --no-delete**: Use `--no-delete` flag when testing
4. **Use --dry-run**: Test operations without making changes
5. **Backup Important Data**: Always maintain backups of critical data

### Permissions

- Run with minimum required permissions
- Don't run as administrator/root unless necessary
- Ensure proper file system permissions on source and replica

### Configuration Files

- Store `dirsync.json` configuration files securely
- Don't commit configuration files with sensitive paths to version control
- Use appropriate file system permissions (e.g., `chmod 600` on Unix)

## Known Limitations

1. **Local File System Only**: DirectorySync is designed for local file systems and local network shares
2. **No Encryption**: Files are copied without encryption (use file system encryption if needed)
3. **No Authentication**: No built-in authentication mechanism (relies on OS-level permissions)

## Dependency Security

- Dependencies are automatically scanned via GitHub's Dependabot
- Security advisories are monitored
- Critical updates are applied promptly

## Contact

For security-related questions (non-vulnerabilities):
- Open a GitHub Discussion
- Email: daniel.kindl@example.com

Thank you for helping keep DirectorySync secure!
