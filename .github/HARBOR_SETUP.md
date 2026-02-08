# Harbor Registry Configuration

## GitHub Secrets Required

### HARBOR_URL
**Value:** `registry.tecklab.dk` (**NO** `https://` prefix!)

**This is the registry endpoint from your HTTPRoute, NOT the Harbor UI URL!**

**❌ Wrong:** 
- `https://registry.tecklab.dk` (no https prefix)
- `harbor.tecklab.dk` (that's the UI, not the registry)
- `https://harbor.tecklab.dk` (wrong on two counts)

**✅ Correct:** `registry.tecklab.dk`

### HARBOR_USERNAME & HARBOR_PASSWORD
Your Harbor credentials or robot account token.

---

## Fix "blob upload invalid" Error

**Cause:** `HARBOR_URL` secret is pointing to wrong endpoint

**Fix:**
1. Go to: Repository → Settings → Secrets and variables → Actions
2. Edit: `HARBOR_URL`
3. Set to: `registry.tecklab.dk` (your HTTPRoute hostname)
4. Save

---

## Harbor URLs Explained

Your cluster has TWO different URLs:

- **Harbor UI:** `harbor.tecklab.dk` (for web browsing, configuration)
- **Registry:** `registry.tecklab.dk` (for docker push/pull via HTTPRoute)

The HTTPRoute `harbor-registry` routes `/v2/*` and `/service/*` to harbor-core service on `registry.tecklab.dk`.

**Workflows must use:** `registry.tecklab.dk`
