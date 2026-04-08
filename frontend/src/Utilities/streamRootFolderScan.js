function parseSseBlocks(buffer) {
  const events = [];
  let rest = buffer;
  let sep;
  while ((sep = rest.indexOf('\n\n')) >= 0) {
    const block = rest.slice(0, sep);
    rest = rest.slice(sep + 2);
    for (const line of block.split('\n')) {
      if (line.startsWith('data:')) {
        const payload = line.slice(5).trimStart();
        if (payload) {
          events.push(JSON.parse(payload));
        }
      }
    }
  }
  return { events, rest };
}

/**
 * Consumes GET /rootFolder/:id/scan-stream (SSE). Invokes onEvent for each JSON payload.
 * @param {number} rootFolderId
 * @param {(evt: object) => void} onEvent
 * @param {{ signal?: AbortSignal }} [options]
 */
export default function streamRootFolderScan(rootFolderId, onEvent, options = {}) {
  const url = `${window.TubeArr.apiRoot}/rootFolder/${rootFolderId}/scan-stream`;

  return fetch(url, {
    method: 'GET',
    headers: {
      'X-Api-Key': window.TubeArr.apiKey,
      Accept: 'text/event-stream'
    },
    signal: options.signal
  }).then(async (res) => {
    if (!res.ok) {
      const err = new Error(`Scan failed (${res.status})`);
      err.status = res.status;
      throw err;
    }

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }
      buffer += decoder.decode(value, { stream: true });
      const { events, rest } = parseSseBlocks(buffer);
      buffer = rest;
      events.forEach(onEvent);
    }

    const tail = parseSseBlocks(buffer + '\n\n');
    tail.events.forEach(onEvent);
  });
}
