import http from "node:http";

const port = Number(process.env.PORT || 4311);
let polls = 0;

function send(response, status, body) {
  response.writeHead(status, {
    "content-type": "application/json",
    "cache-control": "no-store",
  });
  response.end(JSON.stringify(body));
}

const server = http.createServer((request, response) => {
  const url = new URL(request.url || "/", `http://127.0.0.1:${port}`);
  if (request.method === "POST" && url.pathname === "/api/desktop/device/start") {
    polls = 0;
    return send(response, 201, {
      success: true,
      data: {
        deviceCode: "a".repeat(64),
        userCode: "TEST-LOCAL",
        verificationUrl: `http://127.0.0.1:${port}/approved`,
        expiresIn: 600,
        pollInterval: 2,
      },
    });
  }
  if (request.method === "POST" && url.pathname === "/api/desktop/device/token") {
    polls += 1;
    return send(response, 200, {
      success: true,
      data:
        polls < 2
          ? { status: "PENDING" }
          : {
              status: "APPROVED",
              accessToken: `lkd_${"b".repeat(64)}`,
              expiresAt: new Date(Date.now() + 86400000).toISOString(),
            },
    });
  }
  if (request.method === "GET" && url.pathname === "/api/desktop/session") {
    return send(response, 200, {
      success: true,
      data: {
        id: "00000000-0000-0000-0000-000000000001",
        email: "local-test@linkora.test",
        hostnameQuota: 1,
        tokenExpiresAt: new Date(Date.now() + 86400000).toISOString(),
      },
    });
  }
  if (request.method === "GET" && url.pathname === "/api/subdomains/pools") {
    return send(response, 200, {
      success: true,
      data: [
        {
          id: "00000000-0000-0000-0000-000000000010",
          domain: "nx1.lol",
          status: "ACTIVE",
          serviceEndsAt: "2027-07-09T00:00:00.000Z",
        },
      ],
    });
  }
  if (request.method === "GET" && url.pathname === "/api/subdomains/my") {
    return send(response, 200, {
      success: true,
      data: { subdomains: [], quota: 1 },
    });
  }
  if (url.pathname === "/approved") {
    response.writeHead(200, { "content-type": "text/html; charset=utf-8" });
    return response.end("<h1>Mock Linkora authorization approved</h1>");
  }
  return send(response, 404, { success: false, error: "Mock route not found." });
});

server.listen(port, "127.0.0.1", () => {
  console.log(`Linkora Local mock API listening on http://127.0.0.1:${port}`);
});
