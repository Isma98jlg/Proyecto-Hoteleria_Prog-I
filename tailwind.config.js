// Tailwind se compila una vez a Hoteleria-App/wwwroot/css/tailwind.css (ver AGENTS.md):
// el navegador ya no descarga ni ejecuta Tailwind en cada página.
// Paleta formal: azul marino institucional para las acciones, latón como acento y grises fríos de fondo.
/** @type {import('tailwindcss').Config} */
module.exports = {
    darkMode: "class",
    content: [
        "./Hoteleria-App/Pages/**/*.cshtml",
        "./Hoteleria-App/wwwroot/js/**/*.js",
        "./Hoteleria-App/**/*.cs",
        "!./Hoteleria-App/bin/**",
        "!./Hoteleria-App/obj/**"
    ],
    theme: {
        extend: {
            colors: {
                "primary": "#0f2742",
                "on-primary": "#ffffff",
                "primary-container": "#0b1d33",
                "on-primary-container": "#8fa3bf",
                "primary-fixed": "#dbe5f2",
                "primary-fixed-dim": "#b6c6dc",
                "on-primary-fixed": "#0b1d33",
                "on-primary-fixed-variant": "#2b4466",
                "inverse-primary": "#b6c6dc",
                "surface-tint": "#2b4466",

                "secondary": "#8a6418",
                "on-secondary": "#ffffff",
                "secondary-container": "#c9a24e",
                "on-secondary-container": "#241903",
                "secondary-fixed": "#f3e7cc",
                "secondary-fixed-dim": "#e2c88d",
                "on-secondary-fixed": "#2a1d05",
                "on-secondary-fixed-variant": "#5e440f",

                "tertiary": "#1d5c40",
                "on-tertiary": "#ffffff",
                "tertiary-container": "#0f2e20",
                "on-tertiary-container": "#5fae86",
                "tertiary-fixed": "#d6ede1",
                "tertiary-fixed-dim": "#a6d4bd",
                "on-tertiary-fixed": "#0f3d26",
                "on-tertiary-fixed-variant": "#1d5c40",

                "error": "#b42318",
                "on-error": "#ffffff",
                "error-container": "#fde4e1",
                "on-error-container": "#7a1410",

                "background": "#f4f6f9",
                "on-background": "#16202b",
                "surface": "#f4f6f9",
                "surface-bright": "#f8fafc",
                "surface-dim": "#d9dee5",
                "surface-variant": "#dfe4ea",
                "surface-container-lowest": "#ffffff",
                "surface-container-low": "#eef1f5",
                "surface-container": "#e7ebf0",
                "surface-container-high": "#dfe4ea",
                "surface-container-highest": "#d6dce4",
                "on-surface": "#16202b",
                "on-surface-variant": "#4a5563",
                "outline": "#6b7684",
                "outline-variant": "#c3cad3",
                "inverse-surface": "#1f2a37",
                "inverse-on-surface": "#eef1f5"
            },
            borderRadius: {
                DEFAULT: "0.25rem",
                lg: "0.5rem",
                xl: "0.75rem",
                full: "9999px"
            },
            spacing: {
                "gutter": "1.25rem",
                "space-md": "0.75rem",
                "space-lg": "1.25rem",
                "space-xl": "2rem",
                "space-xs": "0.25rem",
                "gutter-desktop": "1.5rem",
                "margin-desktop": "2rem",
                "space-sm": "0.5rem",
                "margin": "1rem"
            },
            fontFamily: Object.fromEntries(
                ["headline-md", "headline-xl", "headline-xl-mobile", "label-md", "body-md", "headline-sm", "label-sm",
                 "numeric-table", "headline-lg", "body-sm", "body-lg", "display-lg", "headline-lg-mobile"]
                    .map(nombre => [nombre, ["Plus Jakarta Sans", "Segoe UI", "system-ui", "sans-serif"]])),
            fontSize: {
                "headline-md": ["1.25rem", { lineHeight: "1.625rem", letterSpacing: "-0.01em", fontWeight: "600" }],
                "headline-xl": ["2.25rem", { lineHeight: "2.75rem", letterSpacing: "-0.025em", fontWeight: "600" }],
                "headline-xl-mobile": ["1.75rem", { lineHeight: "2.25rem", letterSpacing: "-0.02em", fontWeight: "600" }],
                "label-md": ["0.75rem", { lineHeight: "1rem", letterSpacing: "0.04em", fontWeight: "600" }],
                "body-md": ["0.875rem", { lineHeight: "1.375rem", letterSpacing: "0em", fontWeight: "400" }],
                "headline-sm": ["1.125rem", { lineHeight: "1.5rem", letterSpacing: "-0.01em", fontWeight: "600" }],
                "label-sm": ["0.6875rem", { lineHeight: "0.875rem", letterSpacing: "0.06em", fontWeight: "700" }],
                "numeric-table": ["0.875rem", { lineHeight: "1.25rem", letterSpacing: "-0.01em", fontWeight: "500" }],
                "headline-lg": ["1.75rem", { lineHeight: "2.25rem", letterSpacing: "-0.02em", fontWeight: "600" }],
                "body-sm": ["0.8125rem", { lineHeight: "1.25rem", letterSpacing: "0.005em", fontWeight: "400" }],
                "body-lg": ["1rem", { lineHeight: "1.5rem", letterSpacing: "0em", fontWeight: "400" }],
                "display-lg": ["3rem", { lineHeight: "3.5rem", letterSpacing: "-0.03em", fontWeight: "700" }],
                "headline-lg-mobile": ["1.375rem", { lineHeight: "1.75rem", letterSpacing: "-0.015em", fontWeight: "600" }]
            }
        }
    },
    plugins: [require("@tailwindcss/forms"), require("@tailwindcss/container-queries")]
};
