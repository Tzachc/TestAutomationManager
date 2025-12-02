# Test Automation Manager - Documentation Site

This directory contains the documentation micro site for Test Automation Manager.

## Overview

The documentation site is a modern, professional single-page web application that provides comprehensive documentation for all features of the Test Automation Manager application.

## Features

- **Responsive Design**: Built with Bootstrap 5 for mobile-friendly viewing
- **Modern UI**: Professional dark theme with smooth animations
- **Comprehensive Coverage**: Detailed documentation for all application features
- **Interactive Navigation**: Smooth scrolling and active section highlighting
- **Search-Friendly**: Clear structure and organized sections
- **Professional Styling**: Material Design inspired with custom components

## Structure

```
docs/
├── index.html              # Main documentation page
├── assets/
│   ├── css/
│   │   └── style.css      # Custom styles and themes
│   ├── js/
│   │   └── script.js      # Interactive functionality
│   └── images/            # Images and screenshots (optional)
└── README.md              # This file
```

## Integration with Application

The documentation site is integrated into the Test Automation Manager WPF application:

1. **Documentation Link**: A clickable card is located in the left sidebar, right above the "System Status" section
2. **Browser Launch**: Clicking the documentation link opens the site in the default web browser
3. **Automatic Copy**: The docs folder is automatically copied to the output directory during build

## Sections Included

1. **Overview**: Introduction to Test Automation Manager
2. **Getting Started**: Quick start guide for new users
3. **Features**: Overview of core functionality
4. **Tests Management**: Detailed guide for managing test cases
5. **Process Management**: Documentation for automation workflows
6. **Function Library**: Guide for building reusable functions
7. **External Tables**: Managing external data sources
8. **Database Backups**: Backup and restore functionality
9. **Tips & Tricks**: Productivity shortcuts and advanced features
10. **Technical Information**: System requirements and tech stack

## Technology Stack

### Frontend
- **HTML5**: Semantic markup
- **CSS3**: Modern styling with animations
- **JavaScript (ES6+)**: Interactive functionality
- **Bootstrap 5.3.2**: Responsive framework
- **Font Awesome 6.4.2**: Icon library
- **Google Fonts (Inter)**: Professional typography

### Design Principles
- Dark theme matching the application
- Material Design inspired components
- Smooth animations and transitions
- Accessibility-first approach
- Mobile-responsive layout

## Customization

### Updating Content

To update the documentation content:

1. Edit `index.html` to modify sections, add new content, or update existing information
2. The HTML uses semantic sections with IDs for easy navigation
3. Follow the existing structure for consistency

### Styling Changes

To customize the appearance:

1. Edit `assets/css/style.css`
2. CSS variables are defined at the top for easy theme customization:
   ```css
   :root {
       --primary-color: #6366f1;
       --dark-bg: #0f172a;
       --text-primary: #f1f5f9;
       /* ... more variables */
   }
   ```

### Adding New Sections

To add a new documentation section:

1. Add a new `<section>` in `index.html` with a unique ID
2. Add a navigation link in the navbar pointing to the section ID
3. Follow the existing HTML structure for consistency
4. The JavaScript will automatically handle smooth scrolling and active states

## Browser Compatibility

The documentation site is compatible with:
- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Opera 76+

## Performance

- Lightweight: Minimal external dependencies (Bootstrap & Font Awesome from CDN)
- Fast loading: Optimized CSS and JavaScript
- Smooth animations: Hardware-accelerated transforms
- Responsive images: Lazy loading support (if images are added)

## Maintenance

### Regular Updates

Keep the documentation in sync with application features:
- Update version numbers and feature lists as the app evolves
- Add screenshots or GIFs for visual guidance (store in `assets/images/`)
- Review and update technical specifications when dependencies change

### Testing

Before deploying documentation updates:
1. Test all internal links and navigation
2. Verify responsive design on different screen sizes
3. Check browser compatibility
4. Validate HTML and CSS for errors
5. Test the documentation link in the WPF application

## Future Enhancements

Potential improvements for the documentation site:

- [ ] Add search functionality for quick content lookup
- [ ] Include video tutorials or animated GIFs
- [ ] Add a changelog/version history page
- [ ] Implement print-friendly styles
- [ ] Add code syntax highlighting for examples
- [ ] Create a dark/light theme toggle
- [ ] Add interactive demos or sandboxes
- [ ] Include troubleshooting and FAQ sections

## License

This documentation is part of the Test Automation Manager project.

---

## Quick Reference

**File Location**: `TestAutomationManager/docs/`
**Access in App**: Click the "Documentation" card in the left sidebar
**Update Frequency**: As needed when features change
**Owner**: Development Team

For questions or issues with the documentation, please contact the development team or create an issue in the project repository.
