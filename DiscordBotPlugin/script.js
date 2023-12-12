function copyToClipboard(elementId) {
    var textElement = document.getElementById(elementId);
    var text = textElement.innerText;
    navigator.clipboard.writeText(text).then(function () {
        // Change text to "Copied to Clipboard"
        textElement.textContent = 'Copied to Clipboard';
        setTimeout(function () {
            // Revert back to the original text
            textElement.textContent = text;
        }, 2000); // after 2 seconds
    }, function (err) {
        console.error('Could not copy text: ', err);
    });
}

function incrementTime(timeString) {
    let [days, hours, minutes, seconds] = timeString.split(':').map(Number);
    seconds += 1;
    if (seconds >= 60) {
        seconds = 0;
        minutes += 1;
        if (minutes >= 60) {
            minutes = 0;
            hours += 1;
            if (hours >= 24) {
                hours = 0;
                days += 1;
            }
        }
    }
    return [
        days.toString().padStart(2, '0'),
        hours.toString().padStart(2, '0'),
        minutes.toString().padStart(2, '0'),
        seconds.toString().padStart(2, '0')
    ].join(':');
}

function updateUptime() {
    const statusElement = document.querySelector('.status p'); // Get the current status
    const uptimeElement = document.getElementById('uptime');

    // Check if the status text is "Ready" before incrementing the uptime
    if (statusElement && statusElement.textContent.includes('Ready')) {
        uptimeElement.textContent = incrementTime(uptimeElement.textContent);
    }
}

setInterval(updateUptime, 1000); // Call updateUptime every second
