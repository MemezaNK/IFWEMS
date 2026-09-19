import { test, expect } from '@playwright/test';

test.describe('Compliance Check - Org Unit / Supplier / Contract lookups', () => {
  test('org unit, supplier and contract fields render as populated dropdowns', async ({ page }) => {
    await page.goto('/login');
    await page.fill('#username', 'admin');
    await page.fill('#password', 'Admin@12345');
    await page.click('button[type="submit"]');
    await expect(page).not.toHaveURL(/\/login$/, { timeout: 10000 });

    await page.goto('/compliance/check');

    const orgUnitSelect = page.locator('select#orgUnitId');
    await expect(orgUnitSelect).toBeVisible();
    await expect
      .poll(async () => orgUnitSelect.locator('option').count(), { timeout: 10000 })
      .toBeGreaterThan(1);

    const supplierSelect = page.locator('select#supplierId');
    await expect(supplierSelect).toBeVisible();
    expect(await supplierSelect.locator('option').count()).toBeGreaterThanOrEqual(1);

    const contractSelect = page.locator('select#contractId');
    await expect(contractSelect).toBeVisible();
    expect(await contractSelect.locator('option').count()).toBeGreaterThanOrEqual(1);
  });
});
