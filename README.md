# VCRedistInstaller

Install the dreaded VC redistributables on the fly...

E.g use in Electron to be able to include electron-edge:

~~~
try {
  let r = cp.spawnSync(path.resolve(path.dirname(process.argv[0]), "resources", "bin", "VCRedistInstaller.exe"), ['VS2012', 'VS2013', 'VS2015']);
  if (r.status != 0 || r.error) throw new Error(`VCRedistInstaller exited with error code: ${r.status}\nOutput: ${r.stdout}\nError: ${r.stderr}\n${r.error}`);
} catch (err) {
  console.warn(err);
  dialog.showErrorBox("Error installing runtime components", "There was a problem trying to install the Visual Studio Runtime components, correct operation is not guaranteed:\n" + err);
}
~~~
