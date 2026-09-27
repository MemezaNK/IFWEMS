// Production: the SPA is hosted by the IFWEMS API at the site root of its own dedicated IIS
// site/port (base href /), so the relative URL resolves to /api on the same origin.
export const environment = {
  production: true,
  apiBaseUrl: 'api'
};
