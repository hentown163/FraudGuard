// Hide URL Preview on Hover - Global Script
// This script prevents the browser from showing URLs in the status bar when hovering over links

(function() {
    'use strict';
    
    function initializeUrlHiding() {
        // Get all anchor links in the document
        const links = document.querySelectorAll('a[href]');
        
        links.forEach(link => {
            // Store the original href
            const originalHref = link.getAttribute('href');
            
            // Don't process if it's a javascript: or # link
            if (!originalHref || originalHref.startsWith('javascript:') || originalHref === '#') {
                return;
            }
            
            // Store href in data attribute and remove href on mouseenter
            link.addEventListener('mouseenter', function() {
                if (!this.dataset.originalHref) {
                    this.dataset.originalHref = this.getAttribute('href');
                    this.removeAttribute('href');
                }
            });
            
            // Restore href on mouseleave
            link.addEventListener('mouseleave', function() {
                if (this.dataset.originalHref) {
                    this.setAttribute('href', this.dataset.originalHref);
                }
            });
            
            // Handle click event to navigate
            link.addEventListener('click', function(e) {
                if (this.dataset.originalHref) {
                    this.setAttribute('href', this.dataset.originalHref);
                }
                
                const href = this.getAttribute('href') || this.dataset.originalHref;
                
                // Check if it's an external link or needs special handling
                if (href && !href.startsWith('#')) {
                    // Check for middle-click or ctrl/cmd-click (open in new tab)
                    if (e.button === 1 || e.ctrlKey || e.metaKey) {
                        window.open(href, '_blank');
                        e.preventDefault();
                        return;
                    }
                    
                    // Check for shift-click (open in new window)
                    if (e.shiftKey) {
                        window.open(href, '_blank');
                        e.preventDefault();
                        return;
                    }
                    
                    // Normal click - navigate
                    if (this.target === '_blank') {
                        window.open(href, '_blank');
                        e.preventDefault();
                    } else {
                        window.location.href = href;
                        e.preventDefault();
                    }
                }
            });
            
            // Handle focus event (for keyboard navigation)
            link.addEventListener('focus', function() {
                if (this.dataset.originalHref && !this.getAttribute('href')) {
                    this.setAttribute('href', this.dataset.originalHref);
                }
            });
        });
    }
    
    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeUrlHiding);
    } else {
        initializeUrlHiding();
    }
    
    // Re-initialize when new content is loaded dynamically (for SPAs or AJAX content)
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            if (mutation.addedNodes.length > 0) {
                initializeUrlHiding();
            }
        });
    });
    
    // Start observing the document for changes
    observer.observe(document.body, {
        childList: true,
        subtree: true
    });
})();
