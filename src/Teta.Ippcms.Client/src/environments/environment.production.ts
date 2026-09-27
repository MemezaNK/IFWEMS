// Production: the SPA is hosted by the TETA API at the site root of its own dedicated IIS
// site/port (base href /), so the relative URL resolves to /api/v1 on the same origin.
export const environment = {
  production: true,
  apiBaseUrl: 'api/v1'
};
