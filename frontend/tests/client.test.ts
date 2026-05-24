import { describe, it, expect, vi } from 'vitest';
import { api, HttpError } from '../src/api/client';

// Simple unit tests on the typed fetch wrapper. The real REST surface is covered
// end-to-end by the backend's WebApplicationFactory tests.
describe('api client', () => {
  it('serializes array query params as repeated keys', async () => {
    let capturedUrl = '';
    vi.stubGlobal('fetch', vi.fn(async (url: string) => {
      capturedUrl = url;
      return new Response(JSON.stringify({ items: [], total: 0, limit: 50, offset: 0 }), {
        status: 200, headers: { 'content-type': 'application/json' },
      });
    }));
    await api.get('/api/hardware', { category: [1, 2, 3] });
    expect(capturedUrl).toContain('category=1');
    expect(capturedUrl).toContain('category=2');
    expect(capturedUrl).toContain('category=3');
    vi.unstubAllGlobals();
  });

  it('throws HttpError with parsed body on non-2xx', async () => {
    vi.stubGlobal('fetch', vi.fn(async () =>
      new Response(JSON.stringify({ error: 'not_found', detail: 'hardware 999' }), {
        status: 404, headers: { 'content-type': 'application/json' },
      })));
    await expect(api.get('/api/hardware/999')).rejects.toMatchObject({
      status: 404, error: 'not_found', detail: 'hardware 999',
    });
    vi.unstubAllGlobals();
  });

  it('returns undefined on 204 No Content', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 204 })));
    await expect(api.del('/api/hardware/1')).resolves.toBeUndefined();
    vi.unstubAllGlobals();
  });
});
