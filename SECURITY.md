# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x     | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability in Opcilloscope, please report it responsibly:

1. **Do not** open a public GitHub issue
2. Email the maintainers directly or use GitHub's private vulnerability reporting
3. Include as much detail as possible:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)

## Response Timeline

- **Initial response**: Within 48 hours
- **Status update**: Within 7 days
- **Fix timeline**: Depends on severity, typically within 30 days

## Security Considerations

Opcilloscope is an OPC UA client application. When using it:

- **Certificate validation**: By default, development mode auto-accepts certificates. In production environments, configure proper certificate validation.
- **Authentication**: Currently supports anonymous authentication. Use appropriate network security measures.
- **Network exposure**: Opcilloscope connects to OPC UA servers. Ensure you trust the servers you connect to.

## Acknowledgments

We appreciate responsible disclosure and will acknowledge security researchers who report valid vulnerabilities.
