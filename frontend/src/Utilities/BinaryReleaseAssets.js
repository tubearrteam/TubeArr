/**
 * Filter GitHub release assets for the TubeArr host (where binaries run), not the browser.
 * OS/arch come from system status (RuntimeInformation on the server).
 */

function isNoiseAssetName(name) {
  const n = (name || '').toLowerCase();
  return (
    n.includes('sha256') ||
    n.includes('sha2-') ||
    n.includes('checksum') ||
    n.endsWith('.sig') ||
    n.includes('512sums') ||
    n === '_update_spec'
  );
}

/**
 * @param {object} [systemStatus] state.system.status.item from the API
 * @returns {{ os: string, arch: string, label: string, libc?: string }}
 */
export function buildHostBinaryPlatform(systemStatus) {
  const s = systemStatus || {};
  let os = s.hostBinaryPlatformOs;
  if (os !== 'windows' && os !== 'darwin' && os !== 'linux') {
    if (s.isWindows) {
      os = 'windows';
    } else if (s.isOsx) {
      os = 'darwin';
    } else {
      os = 'linux';
    }
  }

  let arch = s.hostBinaryPlatformArch;
  if (arch !== 'arm64' && arch !== 'arm' && arch !== 'x64') {
    arch = 'x64';
  }

  let libc = s.hostBinaryPlatformLibc;
  if (libc !== 'musl' && libc !== 'glibc' && libc !== 'unknown') {
    libc = undefined;
  }

  const osLabel = os === 'windows' ? 'Windows' : os === 'darwin' ? 'macOS' : 'Linux';
  const archLabel = arch === 'arm64' ? 'ARM64' : arch === 'arm' ? 'ARMv7' : 'x64';
  const platform = { os, arch, label: `${osLabel} · ${archLabel}` };
  if (libc) {
    platform.libc = libc;
  }
  return platform;
}

function matchesYtDlpAsset(name, os, arch) {
  const n = (name || '').toLowerCase();
  if (isNoiseAssetName(name)) {
    return false;
  }

  if (os === 'windows') {
    if (n.includes('linux') || n.includes('macos') || n.includes('musl')) {
      return false;
    }
    if (arch === 'arm64') {
      return n.includes('arm64.exe') || n.includes('win_arm64');
    }
    if (arch === 'arm') {
      return false;
    }
    if (n.includes('arm64') || n.includes('aarch64') || n.includes('win_arm64')) {
      return false;
    }
    // x64 Windows runs 32-bit (x86) builds too (WoW64).
    return n.endsWith('.exe') || (n.includes('win') && n.includes('.zip'));
  }

  if (os === 'darwin') {
    return n.includes('macos');
  }

  if (os === 'linux') {
    if (n.includes('win') || n.endsWith('.exe') || n.includes('macos')) {
      return false;
    }
    if (arch === 'arm64') {
      return n.includes('aarch64') || (n.includes('arm64') && !n.includes('armv7'));
    }
    if (arch === 'arm') {
      return n.includes('armv7');
    }
    if (n.includes('aarch64') || n.includes('armv7')) {
      return false;
    }
    return n.includes('linux') || n === 'yt-dlp';
  }

  return false;
}

function looksLikeLinuxArm64Asset(n) {
  return (
    n.includes('aarch64') ||
    n.includes('linuxarm64') ||
    (n.includes('arm64') && !n.includes('amd64'))
  );
}

function matchesFfmpegAsset(name, os, arch) {
  const n = (name || '').toLowerCase();
  if (n.includes('checksum')) {
    return false;
  }

  if (os === 'windows') {
    if (n.includes('linux') && !n.includes('win')) {
      return false;
    }
    if (!n.includes('win')) {
      return false;
    }
    if (arch === 'arm64') {
      return n.includes('winarm64') || n.includes('windowsarm64') || n.includes('arm64');
    }
    // x64/x86: any Windows package except ARM64-only naming
    if (n.includes('winarm64') && !n.includes('win64') && !n.includes('win32')) {
      return false;
    }
    return true;
  }

  if (os === 'linux') {
    if (n.includes('win') || n.endsWith('.exe')) {
      return false;
    }
    if (n.includes('macos') || n.includes('darwin') || n.includes('.app')) {
      return false;
    }
    if (!n.includes('linux') && !n.includes('musl')) {
      return false;
    }
    if (arch === 'arm64') {
      return looksLikeLinuxArm64Asset(n);
    }
    if (arch === 'arm') {
      return n.includes('armv7') || n.includes('armeabi') || n.includes('armhf');
    }
    // x64 (and generic): allow linux/musl without requiring literal "linux64"
    if (looksLikeLinuxArm64Asset(n)) {
      return false;
    }
    if (n.includes('armv7') || n.includes('armeabi') || n.includes('armhf')) {
      return false;
    }
    return true;
  }

  if (os === 'darwin') {
    return (
      n.includes('macos') ||
      n.includes('darwin') ||
      n.includes('osx') ||
      n.includes('apple')
    );
  }

  return false;
}

/** @param {{ os: string, arch: string }} platform */
export function filterYtDlpAssets(assets, platform) {
  const { os, arch } = platform;
  const list = Array.isArray(assets) ? assets : [];
  return list.filter((a) => matchesYtDlpAsset(a.name, os, arch));
}

/** @param {{ os: string, arch: string }} platform */
export function filterFfmpegAssets(assets, platform) {
  const { os, arch } = platform;
  const list = Array.isArray(assets) ? assets : [];
  return list.filter((a) => matchesFfmpegAsset(a.name, os, arch));
}
