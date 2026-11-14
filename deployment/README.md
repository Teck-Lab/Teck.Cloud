# Application deployment manifests

base/ contains reusable application resources. local/, development/, staging/ and
production/ are overlays by environment, never by individual cluster. GitOps references
a versioned overlay and supplies cluster-specific configuration.

Keep application images and manifests tied to the same candidate/release. KIND
tests deploy these manifests with candidate images. Terraform/server provisioning
and shared platform installation belong elsewhere.

No Kubernetes resources are fabricated before an application exists. Add actual
kustomizations with the first application; local overrides must preserve the
behavior the tests claim to verify.
