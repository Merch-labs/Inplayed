function setStatus(text) {
	const el = document.getElementById("status");
	if (el) {
		el.textContent = `Status: ${text}`;
	}
}

function setStats(text) {
	const el = document.getElementById("stats");
	if (el) {
		el.textContent = `Stats: ${text}`;
	}
}

function setNvenc(text) {
	const el = document.getElementById("nvenc");
	if (el) {
		el.textContent = `NVENC: ${text}`;
	}
}

async function fetchStatus() {
	try {
		const status = await window.chrome.webview.hostObjects.backend.GetSessionStatus();
		setStatus(String(status));
	} catch (error) {
		console.error("GetSessionStatus failed:", error);
	}

	try {
		const stats = await window.chrome.webview.hostObjects.backend.GetCaptureStats();
		setStats(String(stats));
	} catch (error) {
		console.error("GetCaptureStats failed:", error);
	}
}

async function probeNvenc() {
	try {
		const info = await window.chrome.webview.hostObjects.backend.GetNvencReadiness();
		setNvenc(String(info));
	} catch (error) {
		console.error("GetNvencReadiness failed:", error);
	}
}

async function startCapture() {
	try {
		await window.chrome.webview.hostObjects.backend.StartCapture();
		await fetchStatus();
	} catch (error) {
		console.error("StartCapture failed:", error);
	}
}

async function stopCapture() {
	try {
		await window.chrome.webview.hostObjects.backend.StopCapture();
		await fetchStatus();
	} catch (error) {
		console.error("StopCapture failed:", error);
	}
}

async function saveClip() {
	try {
		await window.chrome.webview.hostObjects.backend.SaveClip();
		await fetchStatus();
	} catch (error) {
		console.error("SaveClip failed:", error);
	}
}

setInterval(fetchStatus, 1000);
fetchStatus();
probeNvenc();
