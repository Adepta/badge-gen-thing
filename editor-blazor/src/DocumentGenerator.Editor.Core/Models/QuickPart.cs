namespace DocumentGenerator.Editor.Core.Models;

/// <summary>
/// Specifies which code editor a quick part targets.
/// </summary>
public enum QuickPartTarget
{
    /// <summary>Insert into the HTML editor.</summary>
    Html,

    /// <summary>Insert into the CSS editor.</summary>
    Css,

    /// <summary>Can be inserted into either editor.</summary>
    Both
}

/// <summary>
/// A predefined token or code block that can be quickly inserted into the editor.
/// </summary>
/// <param name="Label">Display label shown in the Quick Parts panel.</param>
/// <param name="InsertText">The text to insert at the cursor position.</param>
/// <param name="Category">Top-level category: "Token" or "Block".</param>
/// <param name="Group">Sub-group within the category (e.g. "Attendee", "Branding", "Helpers").</param>
/// <param name="Description">Optional description or tooltip text.</param>
/// <param name="TargetEditor">Which editor this quick part targets.</param>
public record QuickPart(
    string Label,
    string InsertText,
    string Category,
    string Group,
    string? Description = null,
    QuickPartTarget TargetEditor = QuickPartTarget.Html)
{
    /// <summary>
    /// All predefined quick part tokens and blocks matching the existing editor.
    /// </summary>
    public static IReadOnlyList<QuickPart> All { get; } = BuildAll();

    private static List<QuickPart> BuildAll()
    {
        var list = new List<QuickPart>();

        // ── Tokens: Attendee / Variables ──
        list.AddRange(new[]
        {
            new QuickPart("{{variables.firstName}}", "{{variables.firstName}}", "Token", "Attendee", "First name"),
            new QuickPart("{{variables.lastName}}", "{{variables.lastName}}", "Token", "Attendee", "Last name"),
            new QuickPart("{{variables.jobTitle}}", "{{variables.jobTitle}}", "Token", "Attendee", "Job title"),
            new QuickPart("{{variables.company}}", "{{variables.company}}", "Token", "Attendee", "Company name"),
            new QuickPart("{{variables.attendeeId}}", "{{variables.attendeeId}}", "Token", "Attendee", "Attendee ID"),
            new QuickPart("{{variables.ticketType}}", "{{variables.ticketType}}", "Token", "Attendee", "Ticket type"),
            new QuickPart("{{variables.sessionName}}", "{{variables.sessionName}}", "Token", "Attendee", "Session name"),
            new QuickPart("{{variables.eventDate}}", "{{variables.eventDate}}", "Token", "Attendee", "Event date"),
            new QuickPart("{{variables.eventVenue}}", "{{variables.eventVenue}}", "Token", "Attendee", "Event venue"),
        });

        // ── Tokens: Branding ──
        list.AddRange(new[]
        {
            new QuickPart("{{branding.companyName}}", "{{branding.companyName}}", "Token", "Branding", "Company / event name"),
            new QuickPart("{{branding.primaryColour}}", "{{branding.primaryColour}}", "Token", "Branding", "Primary brand colour"),
            new QuickPart("{{branding.secondaryColour}}", "{{branding.secondaryColour}}", "Token", "Branding", "Secondary brand colour"),
            new QuickPart("{{branding.bodyFont}}", "{{branding.bodyFont}}", "Token", "Branding", "Body font family"),
            new QuickPart("{{branding.custom.accentColour}}", "{{branding.custom.accentColour}}", "Token", "Branding", "Accent colour"),
        });

        // ── Tokens: Helpers ──
        list.AddRange(new[]
        {
            new QuickPart("{{{qrCode \u2026}}}", "{{{qrCode variables.attendeeId \"#ffffff\" \"transparent\"}}}", "Token", "Helpers", "QR code SVG"),
            new QuickPart("{{{barCode \u2026}}}", "{{{barCode variables.attendeeId}}}", "Token", "Helpers", "Barcode SVG"),
            new QuickPart("{{upper \u2026}}", "{{upper variables.firstName}}", "Token", "Helpers", "Uppercase text"),
            new QuickPart("{{lower \u2026}}", "{{lower variables.ticketType}}", "Token", "Helpers", "Lowercase text"),
            new QuickPart("{{formatDate \u2026}}", "{{formatDate variables.eventDate \"DD MMM YYYY\"}}", "Token", "Helpers", "Format a date"),
            new QuickPart("{{currency \u2026}}", "{{currency variables.price \"GBP\"}}", "Token", "Helpers", "Format as currency"),
            new QuickPart("{{#ifEquals}}", "{{#ifEquals variables.ticketType \"VIP\"}}VIP content{{/ifEquals}}", "Token", "Helpers", "Conditional block"),
        });

        // ── Tokens: CSS Branding ──
        list.AddRange(new[]
        {
            new QuickPart("primaryColour", "{{branding.primaryColour}}", "Token", "CSS Branding", "Primary colour in CSS", QuickPartTarget.Css),
            new QuickPart("secondaryColour", "{{branding.secondaryColour}}", "Token", "CSS Branding", "Secondary colour in CSS", QuickPartTarget.Css),
            new QuickPart("accentColour", "{{branding.custom.accentColour}}", "Token", "CSS Branding", "Accent colour in CSS", QuickPartTarget.Css),
            new QuickPart("bodyFont", "'{{branding.bodyFont}}', sans-serif", "Token", "CSS Branding", "Body font in CSS", QuickPartTarget.Css),
        });

        // ── Blocks: HTML ──
        list.AddRange(new[]
        {
            new QuickPart("Header bar", "<div class=\"header\">\n  <div class=\"header-left\">\n    <div class=\"event-name\">{{branding.companyName}}</div>\n    <div class=\"event-date\">{{variables.eventDate}}</div>\n    <div class=\"event-venue\">{{variables.eventVenue}}</div>\n  </div>\n  <div class=\"ticket-pill ticket-pill--{{lower variables.ticketType}}\">{{upper variables.ticketType}}</div>\n</div>",
                "Block", "HTML", "Company name, event date and venue, with ticket-type pill"),

            new QuickPart("Name block", "<div class=\"name-block\">\n  <div class=\"first-name\">{{upper variables.firstName}}</div>\n  <div class=\"last-name\">{{upper variables.lastName}}</div>\n  <div class=\"meta-block\">\n    <div class=\"job-title\">{{variables.jobTitle}}</div>\n    <div class=\"company\">{{variables.company}}</div>\n  </div>\n</div>",
                "Block", "HTML", "Large first name, last name, job title and company"),

            new QuickPart("QR footer", "<div class=\"footer\">\n  <div class=\"footer-col\">\n    <div class=\"footer-label\">Attendee ID</div>\n    <div class=\"footer-value mono\">{{variables.attendeeId}}</div>\n    <div class=\"footer-label\" style=\"margin-top:1.5mm\">Session</div>\n    <div class=\"footer-value\">{{variables.sessionName}}</div>\n  </div>\n  <div class=\"footer-divider\"></div>\n  <div class=\"footer-qr\">\n    {{{qrCode variables.attendeeId \"#ffffff\" \"transparent\"}}}\n  </div>\n</div>",
                "Block", "HTML", "Footer strip with attendee ID, session name and QR code"),

            new QuickPart("Diagonal stripe", "<div class=\"stripe-wrap\">\n  <div class=\"stripe\"></div>\n</div>",
                "Block", "HTML", "Accent gradient stripe using brand colours"),

            new QuickPart("Barcode footer", "<div class=\"footer\">\n  <div class=\"barcode-wrap\">\n    {{{barCode variables.attendeeId}}}\n  </div>\n  <div class=\"footer-id mono\">{{variables.attendeeId}}</div>\n</div>",
                "Block", "HTML", "Footer with Code-128 barcode and attendee ID"),
        });

        // ── Blocks: CSS ──
        list.AddRange(new[]
        {
            new QuickPart("Stripe CSS", ".stripe-wrap {\n  position: relative;\n  height: 4mm;\n  flex-shrink: 0;\n  overflow: hidden;\n}\n.stripe {\n  position: absolute;\n  top: 0; left: -10%;\n  width: 120%;\n  height: 100%;\n  background: linear-gradient(90deg,\n    {{branding.custom.accentColour}} 0%,\n    {{branding.primaryColour}} 60%,\n    transparent 100%);\n  transform: skewX(-8deg);\n  transform-origin: left center;\n}",
                "Block", "CSS", "CSS for the diagonal brand-colour accent stripe", QuickPartTarget.Css),

            new QuickPart("Ticket pill CSS", ".ticket-pill {\n  font-size: 6pt; font-weight: 700;\n  letter-spacing: 1.2px; text-transform: uppercase;\n  padding: 1.5mm 3mm;\n  border-radius: 20mm;\n  white-space: nowrap;\n  border: 0.4mm solid transparent;\n}\n.ticket-pill--speaker  { background: {{branding.custom.accentColour}}; color: #0D0D1A; }\n.ticket-pill--vip      { background: transparent; border-color: #D4AF37; color: #D4AF37; }\n.ticket-pill--attendee { background: transparent; border-color: rgba(255,255,255,.35); color: rgba(255,255,255,.7); }\n.ticket-pill--sponsor  { background: #D4AF37; color: #0D0D1A; }\n.ticket-pill--staff    { background: #3B82F6; color: #fff; }",
                "Block", "CSS", "Coloured pill styles for Speaker, VIP, Attendee, Sponsor, Staff", QuickPartTarget.Css),

            new QuickPart("QR footer CSS", ".footer {\n  background: rgba(255,255,255,.04);\n  border-top: 0.3mm solid rgba(255,255,255,.1);\n  padding: 3.5mm 6mm;\n  display: flex; align-items: stretch; gap: 4mm;\n  flex-shrink: 0;\n}\n.footer-col { display: flex; flex-direction: column; justify-content: center; flex: 1; }\n.footer-qr  { flex-shrink: 0; width: 14mm; height: 14mm; }\n.footer-qr svg { width: 100%; height: 100%; display: block; }\n.footer-divider { width: 0.3mm; background: rgba(255,255,255,.12); align-self: stretch; }\n.footer-label {\n  font-size: 5pt; font-weight: 700;\n  text-transform: uppercase; letter-spacing: .8px;\n  color: {{branding.custom.accentColour}}; margin-bottom: .8mm;\n}\n.footer-value { font-size: 7.5pt; font-weight: 600; color: rgba(255,255,255,.8); }\n.mono { font-family: 'Courier New', monospace; letter-spacing: .5px; }",
                "Block", "CSS", "Footer layout, column, divider and QR sizing", QuickPartTarget.Css),
        });

        return list;
    }
}
