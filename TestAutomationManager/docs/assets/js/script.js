/**
 * Test Automation Manager - Documentation Site
 * Interactive JavaScript functionality
 */

// ============================================
// DOCUMENT READY
// ============================================

document.addEventListener('DOMContentLoaded', function() {
    initSmoothScrolling();
    initScrollTopButton();
    initNavbarScrollEffect();
    initActiveNavLinks();
    initAnimations();
});

// ============================================
// SMOOTH SCROLLING FOR ANCHOR LINKS
// ============================================

function initSmoothScrolling() {
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function(e) {
            e.preventDefault();

            const targetId = this.getAttribute('href');
            if (targetId === '#') return;

            const targetElement = document.querySelector(targetId);
            if (targetElement) {
                const navbarHeight = 80;
                const targetPosition = targetElement.offsetTop - navbarHeight;

                window.scrollTo({
                    top: targetPosition,
                    behavior: 'smooth'
                });

                // Close mobile menu if open
                const navbarCollapse = document.querySelector('.navbar-collapse');
                if (navbarCollapse.classList.contains('show')) {
                    navbarCollapse.classList.remove('show');
                }
            }
        });
    });
}

// ============================================
// SCROLL TO TOP BUTTON
// ============================================

function initScrollTopButton() {
    const scrollTopBtn = document.getElementById('scrollTopBtn');

    if (!scrollTopBtn) return;

    // Show/hide button based on scroll position
    window.addEventListener('scroll', function() {
        if (window.pageYOffset > 500) {
            scrollTopBtn.classList.add('show');
        } else {
            scrollTopBtn.classList.remove('show');
        }
    });

    // Scroll to top when clicked
    scrollTopBtn.addEventListener('click', function() {
        window.scrollTo({
            top: 0,
            behavior: 'smooth'
        });
    });
}

// ============================================
// NAVBAR SCROLL EFFECT
// ============================================

function initNavbarScrollEffect() {
    const navbar = document.querySelector('.navbar');
    let lastScroll = 0;

    window.addEventListener('scroll', function() {
        const currentScroll = window.pageYOffset;

        // Add shadow when scrolled
        if (currentScroll > 50) {
            navbar.style.boxShadow = '0 4px 20px rgba(0, 0, 0, 0.5)';
        } else {
            navbar.style.boxShadow = '0 4px 12px rgba(0, 0, 0, 0.3)';
        }

        lastScroll = currentScroll;
    });
}

// ============================================
// ACTIVE NAVIGATION LINKS
// ============================================

function initActiveNavLinks() {
    const sections = document.querySelectorAll('section[id]');
    const navLinks = document.querySelectorAll('.nav-link');

    // Highlight active section in navigation
    window.addEventListener('scroll', function() {
        let current = '';
        const scrollPosition = window.pageYOffset + 100;

        sections.forEach(section => {
            const sectionTop = section.offsetTop;
            const sectionHeight = section.clientHeight;

            if (scrollPosition >= sectionTop && scrollPosition < sectionTop + sectionHeight) {
                current = section.getAttribute('id');
            }
        });

        navLinks.forEach(link => {
            link.classList.remove('active');
            const href = link.getAttribute('href');
            if (href === '#' + current) {
                link.classList.add('active');
            }
        });
    });
}

// ============================================
// SCROLL ANIMATIONS
// ============================================

function initAnimations() {
    // Intersection Observer for fade-in animations
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver(function(entries) {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
            }
        });
    }, observerOptions);

    // Observe elements for animation
    const animateElements = document.querySelectorAll(
        '.feature-card, .showcase-card, .trick-card, .step-item, .feature-detail-card, .tech-card'
    );

    animateElements.forEach((el, index) => {
        el.style.opacity = '0';
        el.style.transform = 'translateY(30px)';
        el.style.transition = `all 0.6s ease ${index * 0.1}s`;
        observer.observe(el);
    });
}

// ============================================
// ACCORDION BEHAVIOR (ENHANCED)
// ============================================

document.querySelectorAll('.accordion-button').forEach(button => {
    button.addEventListener('click', function() {
        // Add smooth transition
        const target = document.querySelector(this.getAttribute('data-bs-target'));
        if (target) {
            setTimeout(() => {
                if (this.classList.contains('collapsed')) {
                    target.style.maxHeight = '0';
                } else {
                    target.style.maxHeight = target.scrollHeight + 'px';
                }
            }, 10);
        }
    });
});

// ============================================
// COPY TO CLIPBOARD FUNCTIONALITY
// ============================================

function addCopyButtons() {
    const codeBlocks = document.querySelectorAll('.code-block');

    codeBlocks.forEach(block => {
        const copyButton = document.createElement('button');
        copyButton.className = 'copy-btn';
        copyButton.innerHTML = '<i class="fas fa-copy"></i>';
        copyButton.title = 'Copy to clipboard';

        copyButton.addEventListener('click', function() {
            const code = block.textContent;
            navigator.clipboard.writeText(code).then(() => {
                copyButton.innerHTML = '<i class="fas fa-check"></i>';
                copyButton.style.color = '#10b981';

                setTimeout(() => {
                    copyButton.innerHTML = '<i class="fas fa-copy"></i>';
                    copyButton.style.color = '';
                }, 2000);
            });
        });

        block.style.position = 'relative';
        block.appendChild(copyButton);
    });
}

// ============================================
// SEARCH FUNCTIONALITY (OPTIONAL ENHANCEMENT)
// ============================================

function initSearch() {
    // This can be enhanced to add search functionality
    const searchInput = document.getElementById('searchInput');
    if (!searchInput) return;

    searchInput.addEventListener('input', function(e) {
        const searchTerm = e.target.value.toLowerCase();

        // Search through content sections
        const sections = document.querySelectorAll('.content-section');
        sections.forEach(section => {
            const text = section.textContent.toLowerCase();
            if (text.includes(searchTerm)) {
                section.style.display = 'block';
            } else {
                section.style.display = 'none';
            }
        });
    });
}

// ============================================
// KEYBOARD SHORTCUTS
// ============================================

document.addEventListener('keydown', function(e) {
    // Escape key to close mobile menu
    if (e.key === 'Escape') {
        const navbarCollapse = document.querySelector('.navbar-collapse.show');
        if (navbarCollapse) {
            navbarCollapse.classList.remove('show');
        }
    }

    // Ctrl/Cmd + K for search (if search is implemented)
    if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        const searchInput = document.getElementById('searchInput');
        if (searchInput) {
            searchInput.focus();
        }
    }
});

// ============================================
// PERFORMANCE OPTIMIZATIONS
// ============================================

// Debounce function for scroll events
function debounce(func, wait = 20, immediate = true) {
    let timeout;
    return function() {
        const context = this;
        const args = arguments;
        const later = function() {
            timeout = null;
            if (!immediate) func.apply(context, args);
        };
        const callNow = immediate && !timeout;
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
        if (callNow) func.apply(context, args);
    };
}

// Apply debounce to scroll-heavy functions
window.addEventListener('scroll', debounce(function() {
    // Scroll event handling
}, 20));

// ============================================
// LAZY LOADING IMAGES (IF NEEDED)
// ============================================

function initLazyLoading() {
    const images = document.querySelectorAll('img[data-src]');

    const imageObserver = new IntersectionObserver((entries, observer) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const img = entry.target;
                img.src = img.dataset.src;
                img.removeAttribute('data-src');
                imageObserver.unobserve(img);
            }
        });
    });

    images.forEach(img => imageObserver.observe(img));
}

// ============================================
// PRINT STYLES HANDLER
// ============================================

function handlePrint() {
    window.addEventListener('beforeprint', function() {
        // Expand all accordion items before printing
        document.querySelectorAll('.accordion-collapse').forEach(collapse => {
            collapse.classList.add('show');
        });
    });

    window.addEventListener('afterprint', function() {
        // Collapse accordion items after printing
        document.querySelectorAll('.accordion-collapse').forEach(collapse => {
            if (!collapse.classList.contains('show')) {
                collapse.classList.remove('show');
            }
        });
    });
}

// ============================================
// DARK MODE TOGGLE (OPTIONAL)
// ============================================

function initDarkModeToggle() {
    const darkModeToggle = document.getElementById('darkModeToggle');
    if (!darkModeToggle) return;

    // Check for saved user preference
    const currentTheme = localStorage.getItem('theme') || 'dark';
    document.documentElement.setAttribute('data-theme', currentTheme);

    darkModeToggle.addEventListener('click', function() {
        const theme = document.documentElement.getAttribute('data-theme');
        const newTheme = theme === 'dark' ? 'light' : 'dark';

        document.documentElement.setAttribute('data-theme', newTheme);
        localStorage.setItem('theme', newTheme);
    });
}

// ============================================
// ANALYTICS (PLACEHOLDER)
// ============================================

function trackEvent(category, action, label) {
    // Placeholder for analytics tracking
    // Can be integrated with Google Analytics, Mixpanel, etc.
    console.log('Event tracked:', category, action, label);
}

// Track navigation clicks
document.querySelectorAll('.nav-link').forEach(link => {
    link.addEventListener('click', function() {
        const section = this.getAttribute('href');
        trackEvent('Navigation', 'Click', section);
    });
});

// ============================================
// UTILITY FUNCTIONS
// ============================================

// Get scroll position
function getScrollPosition() {
    return window.pageYOffset || document.documentElement.scrollTop;
}

// Check if element is in viewport
function isInViewport(element) {
    const rect = element.getBoundingClientRect();
    return (
        rect.top >= 0 &&
        rect.left >= 0 &&
        rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
        rect.right <= (window.innerWidth || document.documentElement.clientWidth)
    );
}

// Smooth scroll to element
function scrollToElement(element, offset = 80) {
    const elementPosition = element.offsetTop - offset;
    window.scrollTo({
        top: elementPosition,
        behavior: 'smooth'
    });
}

// ============================================
// ERROR HANDLING
// ============================================

window.addEventListener('error', function(e) {
    console.error('JavaScript error:', e.message);
    // Could send error to logging service
});

// ============================================
// ACCESSIBILITY ENHANCEMENTS
// ============================================

// Focus management for keyboard navigation
function initAccessibility() {
    // Skip to main content link
    const skipLink = document.querySelector('.skip-to-main');
    if (skipLink) {
        skipLink.addEventListener('click', function(e) {
            e.preventDefault();
            const main = document.querySelector('main') || document.querySelector('#overview');
            if (main) {
                main.focus();
                scrollToElement(main);
            }
        });
    }

    // Ensure interactive elements are keyboard accessible
    document.querySelectorAll('button, a, [tabindex]').forEach(element => {
        if (!element.hasAttribute('tabindex')) {
            element.setAttribute('tabindex', '0');
        }
    });
}

// ============================================
// INITIALIZE ALL FEATURES
// ============================================

function initAll() {
    initAccessibility();
    handlePrint();

    // Add any additional initializations here
    console.log('Test Automation Manager Documentation - Loaded Successfully');
}

// Run initialization
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initAll);
} else {
    initAll();
}

// ============================================
// EXPORT FOR EXTERNAL USE
// ============================================

window.TAMDocs = {
    scrollToElement: scrollToElement,
    trackEvent: trackEvent,
    isInViewport: isInViewport,
    getScrollPosition: getScrollPosition
};
