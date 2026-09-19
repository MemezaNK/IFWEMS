import { test, expect } from '@playwright/test';

async function login(page: import('@playwright/test').Page) {
  await page.goto('/login');
  await page.fill('#username', 'admin');
  await page.fill('#password', 'Admin@12345');
  await page.click('button[type="submit"]');
  await expect(page).not.toHaveURL(/\/login$/, { timeout: 10000 });
}

test.describe('New gap-fix modules', () => {
  test('contracts page loads with list and create form', async ({ page }) => {
    await login(page);
    await page.goto('/contracts');
    await expect(page.locator('h1')).toContainText('Contract Management');
    await expect(page.locator('#supplierId option').first()).toBeAttached();
  });

  test('suppliers page loads with list and create form', async ({ page }) => {
    await login(page);
    await page.goto('/suppliers');
    await expect(page.locator('h1')).toContainText('Supplier Risk Management');
    await expect(page.locator('table.suppliers-table')).toBeVisible();
  });

  test('notifications page loads', async ({ page }) => {
    await login(page);
    await page.goto('/notifications');
    await expect(page.locator('h1')).toContainText('Notifications');
  });

  test('reports page loads with download buttons', async ({ page }) => {
    await login(page);
    await page.goto('/reports');
    await expect(page.locator('h1')).toContainText('Reports');
    await expect(page.locator('button', { hasText: 'Download CSV' }).first()).toBeVisible();
  });

  test('case list has create form and links to case detail', async ({ page }) => {
    await login(page);
    await page.goto('/cases');
    await expect(page.locator('h1')).toHaveText('Case Management');
    await expect(page.locator('#orgUnitId option').first()).toBeAttached();

    await page.selectOption('#caseType', 'IrregularExpenditure');
    await expect(page.locator('#orgUnitId option').nth(1)).toBeAttached({ timeout: 10000 });
    await page.selectOption('#orgUnitId', { index: 1 });
    const uniqueTitle = `E2E Test Case ${Date.now()}`;
    await page.fill('#title', uniqueTitle);
    await expect(page.locator('button[type="submit"]')).toBeEnabled({ timeout: 5000 });
    await page.click('button[type="submit"]');

    const newRow = page.locator('table tbody tr', { hasText: uniqueTitle });
    const errorEl = page.locator('.error');
    await Promise.race([
      newRow.waitFor({ state: 'visible', timeout: 10000 }),
      errorEl.waitFor({ state: 'visible', timeout: 10000 }).catch(() => {})
    ]);
    if (await errorEl.count()) {
      console.log('Create case error:', await errorEl.textContent());
    }
    await expect(newRow).toBeVisible({ timeout: 10000 });
    const openLink = newRow.locator('a', { hasText: 'Open' });
    await expect(openLink).toBeVisible();
    const href = await openLink.getAttribute('href');
    console.log('Open link href:', href);
    await openLink.click();
    await page.waitForURL(/\/cases\/[0-9a-fA-F-]+$/, { timeout: 10000 });
    await expect(page.locator('h1')).toBeVisible();
  });
});
