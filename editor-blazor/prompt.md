# Blazor Template Editor - Implementation Plan

## Overview

Rebuild the existing vanilla JS/Node.js template editor as a **Blazor Server** application on **.NET 10**, with **Monaco Editor**, **Tailwind CSS**, and a **fresh modern SaaS UI design**. The new editor lives at `E:\PoC\DocumentGenerator\editor-blazor\` and is added to the existing `DocumentGenerator.sln`.

---

## Technology Stack

| Layer | Technology | Version |
|---|---|---|
| Runtime | .NET 10 | 10.0.100 |
| UI Framework | Blazor Server (SignalR) | Built-in |
| CSS | Tailwind CSS | 4.x (npm, build step) |
| Code Editor | Monaco Editor | Latest (npm) |
| Template Engine | Handlebars.js | 4.x (JS interop, client-side) |
| Emmet | emmet-monaco-es | Latest (npm) |
| Fonts | Geist + Geist Mono | @fontsource (npm) |
| Storage (primary) | File system | .html/.css/.json on disk |
| Storage (metadata) | SQLite | Microsoft.Data.Sqlite + Dapper |
| Containerization | Docker | Multi-stage (.NET SDK + ASP.NET runtime) |

---

## Theme & Visual Design Specification

### Design Language: Modern SaaS (Linear/Figma-inspired)
- Spacious, clean, rounded corners (radius: 6px default, 8px cards, 12px modals)
- Subtle depth through borders and shadows, not heavy color blocks
- Smooth 200ms transitions on all interactive elements
- Content-first: chrome stays out of the way

### Color System (CSS Custom Properties)

**Dark Mode**
| Token | Hex | Usage |
|---|---|---|
| `--bg-base` | `#0a0a0b` | App background |
| `--bg-surface` | `#141417` | Panels, sidebar, editor bg |
| `--bg-elevated` | `#1c1c21` | Hover, active, popovers |
| `--bg-overlay` | `#232329` | Modals, dropdowns |
| `--border-default` | `#2a2a32` | Panel borders, dividers |
| `--border-subtle` | `#1e1e25` | Inner borders |
| `--text-primary` | `#ededef` | Headings, primary content |
| `--text-secondary` | `#a0a0ab` | Labels, descriptions |
| `--text-tertiary` | `#6b6b76` | Placeholders, disabled |
| `--accent` | `#8b5cf6` | Primary actions, selected |
| `--accent-hover` | `#a78bfa` | Hover on accent |
| `--accent-muted` | `rgba(139,92,246,0.12)` | Selected row tint |
| `--success` | `#22c55e` | Save OK |
| `--warning` | `#f59e0b` | Unsaved indicator |
| `--error` | `#ef4444` | Errors, delete |

**Light Mode**
| Token | Hex | Usage |
|---|---|---|
| `--bg-base` | `#ffffff` | App background |
| `--bg-surface` | `#f8f8fa` | Panels |
| `--bg-elevated` | `#f0f0f3` | Hover, active |
| `--bg-overlay` | `#ffffff` | Modals |
| `--border-default` | `#e2e2e8` | Borders |
| `--border-subtle` | `#ededf0` | Subtle borders |
| `--text-primary` | `#111113` | Primary text |
| `--text-secondary` | `#65656d` | Secondary |
| `--text-tertiary` | `#a0a0a8` | Tertiary |
| `--accent` | `#7c3aed` | Accent (deeper for contrast) |
| `--accent-hover` | `#6d28d9` | Hover |
| `--accent-muted` | `rgba(124,58,237,0.08)` | Muted accent bg |

### Typography
| Element | Font | Size | Weight |
|---|---|---|---|
| UI text | Geist Sans | 13px | 400 |
| Labels | Geist Sans | 12px | 500 |
| Captions | Geist Sans | 11px | 400 |
| Headings | Geist Sans | 16px | 600 |
| Code editor | Geist Mono | 13px | 400 |
| Inline code | Geist Mono | 12px | 400 |
| Status bar | Geist Mono | 11px | 400 |

Line height: 1.5 UI, 1.6 code.

### Layout

```
+----------+----------------------+---------------------+
| SIDEBAR  |     EDITOR PANE      |    PREVIEW PANE     |
| 280px    |                      |                     |
| min:220  |  +----------------+  |  [mode|size|zoom]   |
| max:400  |  | HTML editor    |  |  +---------------+  |
| collapse |  | (Monaco)       |  |  |               |  |
| to 48px  |  |                |  |  |   sandboxed   |  |
|          |  +-- drag --------+  |  |   iframe      |  |
| [search] |  | CSS editor     |  |  |   preview     |  |
| [cards]  |  | (Monaco)       |  |  |               |  |
| [data]   |  +----------------+  |  +---------------+  |
|          |  [layout toggle]     |                     |
+----------+----------------------+---------------------+
| STATUS BAR (28px)                                     |
| file.html . Unsaved | Ln 42, Col 8 | Saved 2m ago ^K |
+-------------------------------------------------------+
```

- **Draggable splitters** between all three columns (positions persisted in localStorage)
- **Sidebar**: 280px default, collapses to 48px icon strip via toggle button
- **Editor/Preview**: 55/45 default split
- **Status bar**: Fixed 28px bottom - file name, dirty dot, cursor position, save state, shortcut hints

### Sidebar - Card Grid
- 2-column grid of template cards
- Card size: ~120x80px, border-radius 8px
- Each card: gradient/color placeholder (or future thumbnail), template name (12px truncated), size pill badge (A6/CC/A4)
- Hover: `scale(1.02)` + shadow elevation
- Selected: accent left border or ring
- Right-click: context menu (Open, Duplicate, Rename, Delete)
- Search bar above grid with filter

### Editor Pane
- Toolbar: layout toggle (vertical/horizontal/tabbed icons), Quick Parts button
- Layout modes saved in settings
- Tab bar visible in tabbed mode, dirty dot per tab
- Quick Parts drawer: slides from right edge, two tabs (Tokens, Blocks)

### Preview Pane
- Toolbar: Editor/Live mode toggle, size preset dropdown, zoom slider (40%-150%), refresh
- Canvas: centered with subtle dot-grid background
- Dimensions displayed below the canvas

### Command Palette (Ctrl+K)
- Centered overlay, 520px wide, backdrop blur
- Search input with icon, grouped results below
- Groups: Recent Templates, Actions, Quick Parts, Settings
- Fuzzy match, arrow-key navigation, Enter to select

### Settings Panel (slide-in from right, 360px)
- Sections: Appearance (theme, font size), Editor (layout, tab size, word wrap, minimap on/off), Preview (default size, auto-refresh delay), Keyboard Shortcuts reference
- All persisted via `SettingsService` -> localStorage

### Modals
- **Quick actions** (Confirm, Rename, Delete): centered, backdrop blur, 420px max, scale+fade animation
- **Complex forms** (New Template, Upload, Import): slide-in right panel, 480px, slide+fade animation

---

## Monaco Editor Language Intelligence

### Custom `handlebars-html` Language
- Extends HTML monarch tokenizer with Handlebars states
- Token colors:
  - `{{variables.*}}` - purple/accent
  - `{{branding.*}}` - teal/cyan
  - `{{#helper}}...{{/helper}}` - orange (keyword)
  - `{{{rawHelper}}}` - warning yellow
  - `{{!-- comments --}}` - grey/tertiary

### Auto-Complete Providers
- **HTML editor**: `{{` triggers completion with all variables, branding tokens, helpers, block helpers (with snippet expansion), plus dynamic keys from current sample data
- **CSS editor**: `{{` in value context suggests branding tokens, context-aware (color tokens for `color:`, font tokens for `font-family:`)

### Real-Time Validation
- Debounced 500ms after last keystroke
- Detects: unclosed `{{`/`}}`, mismatched blocks, unknown helpers
- Displays: red squiggles via `setModelMarkers()`, error count in status bar
- Severity: Error (red), Warning (yellow), Info (blue)

### Emmet Support
- `emmet-monaco-es` registered for `handlebars-html`
- Tab to expand abbreviations

### Custom Monaco Themes
- `editor-dark` and `editor-light` matching the app color palette
- Full token color map for HTML, CSS, Handlebars expressions, strings, comments, numbers

---

## Solution Structure

```
editor-blazor/
  DocumentGenerator.Editor.sln          # Standalone sln (also added to root DocumentGenerator.sln)

  src/
    DocumentGenerator.Editor.Core/          # Domain models, interfaces, DTOs (no dependencies)
      Models/
        Template.cs                     # Template entity (name, html, css, family, sizePreset, timestamps)
        TemplateFamily.cs               # Enum: Pulse, Executive, Carbon, Invoice, Custom
        SizePreset.cs                   # Enum: A6, CreditCard, A4, Custom + dimensions
        SampleData.cs                   # Dictionary wrapper with flat/nested conversion
        Asset.cs                        # Asset entity (filename, size, url, contentType)
        QuickPart.cs                    # Token or block definition
        EditorSettings.cs              # User preferences model
      Interfaces/
        ITemplateRepository.cs          # CRUD for templates (file system)
        IAssetRepository.cs             # Upload, list, delete assets
        ISampleDataRepository.cs        # Load/save sample JSON
        IMetadataStore.cs               # SQLite metadata (indexing, search, timestamps)
      DTOs/
        TemplateListItem.cs             # Sidebar card DTO (name, family, size, thumbnail, lastModified)
        TemplateSaveRequest.cs          # HTML + CSS + sample data
        RenameRequest.cs                # Old name -> new name

    DocumentGenerator.Editor.Infrastructure/  # File system, SQLite, services
      FileSystem/
        FileTemplateRepository.cs     # ITemplateRepository impl - reads/writes .html/.css/.json
        FileAssetRepository.cs        # IAssetRepository impl - stores images on disk
      Database/
        SqliteMetadataStore.cs        # IMetadataStore impl - template index, timestamps, thumbnails
        Migrations/
          001_InitialSchema.sql       # Schema creation/migration scripts
      Services/
        TemplateService.cs            # Orchestrates file + metadata operations
        AssetService.cs               # Validates, stores, and indexes assets
        SampleDataService.cs          # Manages sample JSON files

    DocumentGenerator.Editor.Web/            # Blazor Server application
      Program.cs                      # DI setup, SignalR config, middleware
      appsettings.json                # Paths, ports, SQLite connection
      appsettings.Development.json
      Dockerfile
      package.json                    # npm: tailwindcss, monaco-editor, emmet, fonts
      tailwind.config.js
      postcss.config.js

      wwwroot/
        css/
          app.css                     # Tailwind input (with @layer directives)
          app.min.css                 # Tailwind output (generated at build)
          themes.css                  # CSS custom properties for dark/light
        js/
          monaco-interop.js           # Core: create, dispose, get/set, resize
          monaco-languages.js         # Custom handlebars-html monarch tokenizer
          monaco-completions.js       # Token auto-complete (HTML + CSS)
          monaco-validation.js        # Handlebars validator + inline error markers
          monaco-themes.js            # Custom editor-dark / editor-light themes
          handlebars-interop.js       # Compile templates, register helpers
          emmet-interop.js            # Emmet init for handlebars-html
          splitter-interop.js         # Panel resize
          clipboard-interop.js        # Copy-to-clipboard
        lib/
          monaco-editor/              # Monaco dist (copied from node_modules at build)

      Components/
        App.razor                     # Root component
        _Imports.razor                # Global usings
        Routes.razor                  # Router

        Layout/
          MainLayout.razor            # Three-column layout with splitters
          StatusBar.razor             # Bottom status bar

        Pages/
          Editor.razor                # Main editor page (the only real "page")

        Sidebar/
          Sidebar.razor               # Sidebar container (collapsible)
          TemplateSearch.razor        # Search/filter bar
          TemplateCardGrid.razor      # Card grid of templates
          TemplateCard.razor          # Individual template card
          SampleDataPanel.razor       # Collapsible sample data editor

        Editor/
          EditorPane.razor            # Editor container with layout toggle
          MonacoEditor.razor          # Monaco wrapper component (JS interop)
          EditorToolbar.razor         # Layout toggle + Quick Parts trigger
          QuickPartsDrawer.razor      # Slide-in Quick Parts panel (tokens + blocks)

        Preview/
          PreviewPane.razor           # Preview container
          PreviewToolbar.razor        # Mode, size, zoom controls
          PreviewCanvas.razor         # Sandboxed iframe with Blob URL

        Shared/
          CommandPalette.razor        # Ctrl+K command palette overlay
          SettingsPanel.razor         # Slide-in settings panel
          Modal.razor                 # Reusable centered modal
          SlidePanel.razor            # Reusable slide-in panel
          ConfirmDialog.razor         # Confirm action modal
          ContextMenu.razor           # Right-click context menu

        Dialogs/
          NewTemplateDialog.razor     # Slide-in: create new template
          RenameDialog.razor          # Modal: rename template
          UploadAssetsDialog.razor    # Slide-in: drag-and-drop asset upload
          ImportDialog.razor          # Slide-in: import from disk

      Services/
        EditorState.cs               # Central state management (scoped, SignalR-friendly)
        ThemeService.cs              # Dark/light/system theme management
        KeyboardShortcutService.cs   # Registers global shortcuts
        MonacoInteropService.cs      # C# wrapper for monaco-interop.js
        MonacoLanguageService.cs     # Register custom language, update token definitions
        MonacoCompletionService.cs   # Push dynamic completion items
        MonacoValidationService.cs   # Trigger validation, receive error markers
        HandlebarsInteropService.cs  # C# wrapper for handlebars-interop.js
        PreviewService.cs            # Debounced preview rendering logic
        SettingsService.cs           # Read/write user preferences to localStorage

  tests/
    DocumentGenerator.Editor.Tests/
      DocumentGenerator.Editor.Tests.csproj
      Services/
        TemplateServiceTests.cs
        SampleDataServiceTests.cs
        AssetServiceTests.cs
      Infrastructure/
        FileTemplateRepositoryTests.cs
        SqliteMetadataStoreTests.cs
```

---

## Feature Parity Checklist (vs. existing editor)

| Feature | Existing | Blazor | Notes |
|---|---|---|---|
| List templates | Yes | Yes | Card grid instead of grouped list |
| Create template | Yes | Yes | Slide-in panel with presets |
| Open/load template | Yes | Yes | Click card or Ctrl+K search |
| Save (Ctrl+S) | Yes | Yes | Via SignalR to server |
| Duplicate | Yes | Yes | Context menu or command palette |
| Rename | Yes | Yes | Modal dialog |
| Delete | Yes | Yes | Confirm modal |
| Import .html/.css | Yes | Yes | Slide-in panel |
| Export .zip | Yes | Yes | Server-side ZipArchive |
| Dual code editors | Yes | Yes | Monaco with configurable layout |
| Syntax highlighting | Basic (CM5) | Full (Monaco) | + Handlebars tokens |
| Auto-complete | No | **NEW** | Handlebars tokens + CSS tokens |
| Error markers | No | **NEW** | Inline Handlebars validation |
| Emmet | No | **NEW** | HTML abbreviation expansion |
| Editor/Live preview | Yes | Yes | Same approach via Handlebars.js |
| Size presets + zoom | Yes | Yes | A6, CC, A4, Custom + 40-150% zoom |
| Quick Parts (Ctrl+Q) | Yes | Yes | Drawer with tokens + blocks |
| Sample data editing | Yes | Yes | Collapsible sidebar panel |
| Asset upload | Yes | Yes | Drag-drop slide-in panel |
| Dark/light theme | Yes | Yes | + system preference detection |
| Unsaved changes guard | Yes | Yes | NavigationLock + beforeunload |
| Keyboard shortcuts | 3 | 4+ | + Ctrl+K command palette |
| Command palette | No | **NEW** | Ctrl+K fuzzy search |
| Settings panel | No | **NEW** | Full preferences UI |
| Status bar | No | **NEW** | Persistent info bar |
| Context menu | No | **NEW** | Right-click on cards |
| Configurable editor layout | No | **NEW** | Vertical/horizontal/tabbed |

---

## Implementation Phases

### Phase 1: Project Scaffolding & Foundation
1. Create `editor-blazor/` directory
2. `dotnet new blazor` for the Web project (server render mode)
3. `dotnet new classlib` for Core and Infrastructure projects
4. `dotnet new xunit` for Tests project
5. Create `DocumentGenerator.Editor.sln` and add all 4 projects
6. Add the 3 `src/` projects to root `DocumentGenerator.sln` under an `editor-blazor` solution folder
7. Set up `package.json` in Web project (tailwindcss, postcss, autoprefixer, monaco-editor, @fontsource/geist-sans, @fontsource/geist-mono, emmet-monaco-es, handlebars)
8. Configure Tailwind (`tailwind.config.js`, `postcss.config.js`, `app.css` with `@tailwind` directives + custom theme)
9. Add MSBuild targets in `.csproj` to run `npm install` + `npx tailwindcss` at build time, and copy Monaco dist to `wwwroot/lib/`
10. Create `themes.css` with all CSS custom properties (dark + light)
11. Create `MainLayout.razor` skeleton (three columns + status bar placeholder)
12. Implement `ThemeService` (detect `prefers-color-scheme`, toggle, persist to localStorage)
13. Verify the app runs: `dotnet run` shows the themed three-column skeleton
14. Create `Dockerfile` (multi-stage: SDK build with npm -> ASP.NET runtime)

### Phase 2: Core Domain & Infrastructure
1. Define models: `Template`, `TemplateFamily`, `SizePreset`, `SampleData`, `Asset`, `QuickPart`, `EditorSettings`
2. Define interfaces: `ITemplateRepository`, `IAssetRepository`, `ISampleDataRepository`, `IMetadataStore`
3. Define DTOs: `TemplateListItem`, `TemplateSaveRequest`, `RenameRequest`
4. Implement `FileTemplateRepository` (list, read, write, delete, rename .html/.css files with path traversal protection)
5. Implement `FileAssetRepository` (upload with validation, list, serve, delete with file type/size restrictions)
6. Implement `SampleDataService` (load/save .json, flat<->nested conversion)
7. Set up SQLite: add `Microsoft.Data.Sqlite` + `Dapper` NuGet packages
8. Create `001_InitialSchema.sql` (templates table: name, family, sizePreset, lastModified, thumbnail)
9. Implement `SqliteMetadataStore` (auto-migrate on startup, CRUD, search by name/family)
10. Implement `TemplateService` (coordinates file repo + metadata store)
11. Implement `AssetService` (validates, stores, indexes)
12. Wire up DI in `Program.cs` (register all services, configure paths from `appsettings.json`)
13. Write unit tests for `TemplateService`, `SampleDataService`, `FileTemplateRepository`

### Phase 3: Monaco Editor Integration
1. Create `monaco-interop.js` - core functions: `createEditor(elementId, language, theme, value, options)`, `dispose(editorId)`, `getValue(editorId)`, `setValue(editorId, value)`, `setTheme(theme)`, `onDidChangeContent(editorId, dotnetRef)`, `getCursorPosition(editorId)`, `setCursorPosition(editorId, line, col)`, `insertText(editorId, text)`, `layout(editorId)` (resize)
2. Create `monaco-themes.js` - define `editor-dark` and `editor-light` with full token color rules
3. Create `monaco-languages.js` - register `handlebars-html` language with Monarch tokenizer extending HTML with `{{...}}` states
4. Create `monaco-completions.js` - `CompletionItemProvider` for `handlebars-html` (variables, branding, helpers, block snippets) + CSS token completions + function to update dynamic tokens from C#
5. Create `monaco-validation.js` - parse Handlebars expressions, detect errors, call `setModelMarkers()`, report error count back to C#
6. Create `emmet-interop.js` - initialize `emmet-monaco-es` for `handlebars-html`
7. Build `MonacoInteropService.cs` - C# async wrappers for all `monaco-interop.js` functions via `IJSRuntime`
8. Build `MonacoLanguageService.cs` - register language on first editor creation
9. Build `MonacoCompletionService.cs` - push updated token lists from sample data changes
10. Build `MonacoValidationService.cs` - receive error markers, expose error count for status bar
11. Build `MonacoEditor.razor` component - renders `<div>`, calls `createEditor` in `OnAfterRenderAsync`, handles disposal, exposes `Value`, `Language`, `OnContentChanged` parameters
12. Build `EditorPane.razor` - configurable layout (vertical/horizontal/tabbed), two `MonacoEditor` instances (HTML + CSS), draggable splitter between them
13. Build `EditorToolbar.razor` - layout toggle buttons, Quick Parts trigger
14. Create `splitter-interop.js` for drag-to-resize panels
15. Verify: editors render, syntax highlighting works, typing triggers change events

### Phase 4: Sidebar & Template Management
1. Build `Sidebar.razor` - collapsible container (280px <-> 48px), collapse toggle button
2. Build `TemplateSearch.razor` - search input with filter icon, debounced text change
3. Build `TemplateCardGrid.razor` - 2-column CSS grid, receives filtered list, renders cards
4. Build `TemplateCard.razor` - gradient placeholder, name, size pill, hover/selected states
5. Build `ContextMenu.razor` - reusable right-click menu (position at pointer, auto-close on click-away)
6. Wire sidebar to `TemplateService` - load template list on init, refresh after mutations
7. Implement template open flow: card click -> check dirty -> load HTML+CSS+JSON into editors
8. Implement save flow: Ctrl+S -> `EditorState` -> `TemplateService.Save()` -> update status bar
9. Build `NewTemplateDialog.razor` (slide-in) - name input, size preset radio buttons, family dropdown, scaffold boilerplate
10. Build `RenameDialog.razor` (centered modal) - current name display, new name input, validate
11. Implement duplicate: clone template via `TemplateService`, open the copy
12. Implement delete: confirm modal -> `TemplateService.Delete()` -> refresh sidebar
13. Build `EditorState.cs` - central scoped service: `CurrentTemplate`, `IsDirty`, `HtmlContent`, `CssContent`, `PreviewMode`, `SampleData`, event delegates for state changes
14. Implement unsaved changes guard using Blazor's `NavigationLock` component + `beforeunload` via JS interop

### Phase 5: Preview System
1. Create `handlebars-interop.js` - `registerHelpers()` (qrCode, barCode, upper, lower, formatDate, currency, ifEquals), `compile(html, data)`, `resolveCssTokens(css, data)`
2. Build `HandlebarsInteropService.cs` - C# async wrappers
3. Build `PreviewCanvas.razor` - sandboxed `<iframe>`, Blob URL rendering with `URL.createObjectURL`, memory cleanup (`revokeObjectURL`), centered on dot-grid background
4. Build `PreviewToolbar.razor` - Editor/Live mode toggle, size preset dropdown (A6: 105x148mm, CC: 85.6x54mm, A4: 210x297mm, Custom), zoom slider (40-150%), refresh button
5. Build `PreviewPane.razor` - combines toolbar + canvas, auto-size detection from template name
6. Build `PreviewService.cs` - debounced rendering (280ms), Editor mode (raw HTML + CSS token resolution), Live mode (Handlebars compile with sample data), calls `HandlebarsInteropService`
7. Wire `EditorState.OnContentChanged` -> `PreviewService.ScheduleRender()`
8. Verify: editing HTML/CSS updates preview in both modes

### Phase 6: Sample Data & Quick Parts
1. Build `SampleDataPanel.razor` - collapsible section in sidebar, key-value rows with inline editing, add/remove field buttons, reset-to-defaults button, save button
2. Wire to `SampleDataService` - auto-load matching `sample-{name}.json` on template open, save on explicit action
3. Wire sample data changes -> `MonacoCompletionService.UpdateDynamicTokens()` (so new fields appear in auto-complete)
4. Wire sample data changes -> `PreviewService.ScheduleRender()` (re-render in Live mode)
5. Build `QuickPartsDrawer.razor` - slides in from right of editor pane, two tabs (Tokens, Blocks)
6. Implement Tokens tab: 4 groups (Attendee, Branding, Helpers, CSS branding), click inserts at Monaco cursor via `MonacoInteropService.InsertText()`
7. Implement Blocks tab: pre-built snippets (Header bar, Name block, QR footer, etc.), click inserts HTML or CSS snippet into the appropriate editor
8. Wire Ctrl+Q to toggle drawer

### Phase 7: Command Palette & Settings
1. Build `CommandPalette.razor` - centered overlay (520px), search input, grouped results, fuzzy matching
2. Implement groups: Recent Templates (last 5 opened), Actions (New, Save, Duplicate, Rename, Delete, Import, Export), Quick Parts (all tokens), Settings shortcuts
3. Arrow key navigation + Enter to execute, Escape to close
4. Wire Ctrl+K to open/close
5. Build `SettingsPanel.razor` - slide-in from right (360px), sections with form controls
6. Implement sections:
   - Appearance: theme (dark/light/system), UI font size
   - Editor: layout (V/H/tabs), tab size (2/4), word wrap on/off, minimap on/off, line numbers on/off
   - Preview: default size preset, auto-refresh on/off, debounce delay
   - Keyboard Shortcuts: read-only reference list
7. Build `SettingsService.cs` - serializes `EditorSettings` to/from localStorage via JS interop
8. Build `KeyboardShortcutService.cs` - registers `keydown` listener via JS interop, dispatches to appropriate Blazor handlers (Ctrl+S, Ctrl+K, Ctrl+Q, Escape)

### Phase 8: Import/Export & Asset Upload
1. Build `ImportDialog.razor` (slide-in) - file picker for .html/.css, preview of file name + size, import button loads into appropriate editor
2. Implement export: server-side `System.IO.Compression.ZipArchive` - packages .html + .css + .json, returns as file download via `Content-Disposition`
3. Build `UploadAssetsDialog.razor` (slide-in) - drag-and-drop zone, file picker, multi-file (max 10), type validation (png/jpg/gif/svg/webp), size validation (max 5MB), per-file upload progress via streaming
4. Wire to `AssetService` - upload via `IBrowserFile` (Blazor), store and index

### Phase 9: Polish & Responsive Design
1. Add transitions/animations: modal enter/exit (scale+fade, 200ms), slide panel (translateX, 200ms), card hover (scale, 150ms), toast fade
2. Responsive breakpoints:
   - `<640px`: sidebar collapses to icon strip, status bar stacks
   - `<960px`: editor and preview stack vertically instead of side-by-side
   - `<1200px`: preview pane collapsible
3. Status bar wiring: current file name + dirty dot (left), cursor Ln:Col from Monaco (center), save status + last saved time + Ctrl+K/Ctrl+Q hints (right)
4. Error handling: global error boundary, service-level try/catch with status bar error display
5. Loading states: skeleton placeholders for sidebar cards, editor loading spinner
6. SignalR reconnection: overlay with "Reconnecting..." message and auto-retry
7. Accessibility: keyboard navigation for cards/menus, ARIA labels, focus management for modals

### Phase 10: Docker & Testing
1. Finalize `Dockerfile`:
   - Stage 1: `mcr.microsoft.com/dotnet/sdk:10.0` + Node.js (for npm build), restore, build, publish
   - Stage 2: `mcr.microsoft.com/dotnet/aspnet:10.0`, copy publish output, configure ports/paths
2. Add `editor-blazor` service to root `docker-compose.yml` with volume mount for templates directory
3. Unit tests: `TemplateService`, `SampleDataService`, `AssetService`, `FileTemplateRepository`, `SqliteMetadataStore`
4. Integration tests: full template CRUD lifecycle, asset upload/list, sample data round-trip
5. Verify Docker build and run

---

## Configuration

### `appsettings.json`
```json
{
  "Editor": {
    "TemplatesDir": "../templates",
    "AssetsDir": "../templates/assets",
    "SqliteConnectionString": "Data Source=editor-metadata.db"
  },
  "AllowedHosts": "*"
}
```

### Environment Variable Overrides
| Variable | Default | Description |
|---|---|---|
| `EDITOR__TEMPLATESDIR` | `../templates` | Path to template files |
| `EDITOR__ASSETSDIR` | `{TemplatesDir}/assets` | Path for uploaded images |
| `EDITOR__SQLITECONNECTIONSTRING` | `Data Source=editor-metadata.db` | SQLite DB path |
| `ASPNETCORE_URLS` | `http://+:3500` | Listen URL |

### NuGet Packages
| Package | Project | Purpose |
|---|---|---|
| `Microsoft.Data.Sqlite` | Infrastructure | SQLite access |
| `Dapper` | Infrastructure | Lightweight ORM |

### npm Packages (in Web project)
| Package | Purpose |
|---|---|
| `tailwindcss` | CSS framework |
| `@tailwindcss/postcss` | PostCSS plugin |
| `postcss` | CSS processing |
| `autoprefixer` | Vendor prefixes |
| `monaco-editor` | Code editor |
| `emmet-monaco-es` | Emmet abbreviation support |
| `@fontsource/geist-sans` | UI font |
| `@fontsource/geist-mono` | Code font |
| `handlebars` | Template compilation (browser) |
