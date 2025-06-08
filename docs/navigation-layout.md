# Navigation Layout

## Original Implementation (June 2025)

The navigation layout in FeedbackFlow initially used a responsive design with different layouts for desktop and mobile:

### Desktop Layout
- Main navigation items (Analyze, Feeds, Reports) shown directly in the navbar
- Secondary items grouped under a "More" dropdown menu (desktop only):
  - Saved
  - What's New
  - Settings

### Mobile Layout
- Main navigation items shown directly
- Secondary items shown as full menu items (no dropdown)
- All items include both icons and text labels

### Implementation Details
- Uses Bootstrap's navbar components
- Responsive classes (`d-lg-none` and `d-none d-lg-block`) to control visibility
- Icons from Bootstrap Icons library
- Accessibility features:
  - Proper ARIA attributes on dropdown
  - Clear visual indicators for active states
  - Consistent icon + text pattern for all items

```html
<!-- Example of original desktop dropdown implementation -->
<li class="nav-item dropdown d-none d-lg-block">
    <a class="nav-link dropdown-toggle" href="#" role="button" data-bs-toggle="dropdown" aria-expanded="false">
        <i class="bi bi-three-dots-vertical"></i>
        <span>More</span>
    </a>
    <ul class="dropdown-menu dropdown-menu-end">
        <!-- Secondary navigation items -->
    </ul>
</li>
```

## Updated Implementation (June 2025)

The navigation layout was simplified to provide a more consistent experience across all device sizes:

### New Layout Features
- Main navigation items (Analyze, Feeds, Reports) remain unchanged
- Secondary items now appear directly in the navbar for all screen sizes:
  - Saved (shows both icon and text)
  - What's New (shows both icon and text)
  - Settings (shows both icon and text)

### Accessibility Improvements
- All navigation items now show full text labels for better usability
- Consistent icon + text pattern for all items
- Proper active state indicators
- Better touch targets for mobile devices

### Implementation Details
```html
<!-- Example of updated implementation -->
<li class="nav-item">
    <a class="nav-link @(IsActive("/settings") ? "active" : "")" href="/settings">
        <i class="bi bi-gear me-1"></i>
        Settings
    </a>
</li>
```

### Key Changes from Original
1. Removed the "More" dropdown menu
2. Eliminated separate mobile/desktop layouts
3. Simplified navigation structure
4. All items now show full text labels
5. Consistent spacing and alignment

## Current Implementation (June 2025)

The navigation was further enhanced with a responsive hamburger menu system:

### Responsive Behavior
- **Desktop (≥992px)**: Shows full horizontal navigation with all items visible
- **Mobile/Tablet (<992px)**: Shows hamburger dropdown menu on left side of logo

### Desktop Layout
- All navigation items displayed horizontally in the navbar
- Theme toggle on the far right
- Clean, uncluttered design

### Mobile Layout
- Hamburger menu icon (`bi-list`) positioned to the left of the logo
- Dropdown menu contains all navigation items with icons and text
- Regular navigation items hidden on smaller screens
- Organized with divider separating main and secondary items

### Implementation Details

#### Hamburger Button
```html
<div class="navbar-nav me-3 d-lg-none">
    <div class="nav-item dropdown">
        <button class="nav-link dropdown-toggle btn btn-link p-0 border-0" 
                type="button" data-bs-toggle="dropdown" aria-expanded="false">
            <i class="bi bi-list"></i>
        </button>
        <ul class="dropdown-menu">
            <!-- All navigation items -->
        </ul>
    </div>
</div>
```

#### Responsive CSS
```css
/* Left-side dropdown navigation - only show on smaller screens */
.navbar-nav .nav-item.dropdown .nav-link.dropdown-toggle {
    color: rgba(255, 255, 255, 0.85);
    font-size: 1.1rem;
    padding: 0.375rem 0.5rem;
    border-radius: var(--border-radius);
    width: 44px;
    height: 44px;
    display: flex;
    align-items: center;
    justify-content: center;
}

/* Regular navigation hidden on mobile */
.collapse.navbar-collapse {
    /* Hidden on screens < 992px via d-none d-lg-block classes */
}
```

### Key Features
1. **Space-efficient**: Hamburger menu saves screen real estate on mobile
2. **Consistent access**: All navigation items available in dropdown
3. **Proper sizing**: 44px button provides good touch target
4. **Theme integration**: Matches existing design system
5. **Bootstrap integration**: Uses standard Bootstrap dropdown components
6. **Active states**: Current page highlighted in dropdown menu

### Navigation Organization
The dropdown menu organizes items logically:
- **Main actions**: Analyze, Feeds, Reports
- **Divider**: Visual separation
- **Secondary actions**: Saved, What's New, Settings
