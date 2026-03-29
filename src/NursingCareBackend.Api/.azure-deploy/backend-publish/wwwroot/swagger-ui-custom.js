// Custom Swagger UI enhancement to add JWT Authorize button
(function() {
  function setupJwtAuthorization() {
    // Wait for Swagger UI to fully load
    setTimeout(function() {
      // Create the authorize button if it doesn't exist
      const topbar = document.querySelector('.topbar');
      if (topbar && !document.querySelector('#jwt-authorize-btn')) {
        const authorizeBtn = document.createElement('button');
        authorizeBtn.id = 'jwt-authorize-btn';
        authorizeBtn.className = 'btn authorize unlocked';
        authorizeBtn.innerHTML = '🔓 Authorize';
        authorizeBtn.style.cssText = `
          margin-left: auto;
          margin-right: 10px;
          padding: 10px 20px;
          background-color: #49cc90;
          color: #fff;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-size: 14px;
          font-weight: bold;
        `;

        authorizeBtn.onclick = function(e) {
          e.preventDefault();
          showJwtModal();
        };

        // Add button to topbar
        const logo = topbar.querySelector('.logo');
        if (logo && logo.parentNode) {
          logo.parentNode.insertBefore(authorizeBtn, logo.nextSibling);
        } else {
          topbar.appendChild(authorizeBtn);
        }
      }

      // Store JWT token in localStorage
      window.jwtToken = localStorage.getItem('jwt_token') || '';
      
      // Intercept Fetch API to add JWT header
      const originalFetch = window.fetch;
      window.fetch = function(...args) {
        let resource = args[0];
        let config = args[1] || {};
        const url = typeof resource === 'string' ? resource : resource.url;
        
        // Add Authorization header for API calls
        if (window.jwtToken && url && url.includes('/api/')) {
          config.headers = {
            ...(config.headers || {}),
            'Authorization': `Bearer ${window.jwtToken}`
          };
        }
        
        return originalFetch.call(this, resource, config);
      };
      
      // Also intercept XMLHttpRequest to add JWT header (fallback)
      const originalOpen = XMLHttpRequest.prototype.open;
      XMLHttpRequest.prototype.open = function(method, url, ...rest) {
        this._url = url;
        return originalOpen.apply(this, [method, url, ...rest]);
      };

      const originalSend = XMLHttpRequest.prototype.send;
      XMLHttpRequest.prototype.send = function(data) {
        if (window.jwtToken && this._url && this._url.includes('/api/')) {
          this.setRequestHeader('Authorization', `Bearer ${window.jwtToken}`);
        }
        return originalSend.apply(this, [data]);
      };

    }, 1000);
  }

  function showJwtModal() {
    const token = window.jwtToken || '';
    
    const html = `
      <div id="jwt-modal-overlay" style="
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background-color: rgba(0, 0, 0, 0.5);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 10000;
      ">
        <div style="
          background: white;
          border-radius: 8px;
          padding: 30px;
          max-width: 600px;
          width: 90%;
          box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
        ">
          <h2 style="margin-top: 0; color: #333;">JWT Authorization</h2>
          <p style="color: #666;">Enter your JWT token below to authorize requests:</p>
          
          <textarea id="jwt-token-input" placeholder="Enter your JWT token here (without 'Bearer ' prefix)" style="
            width: 100%;
            height: 120px;
            padding: 10px;
            border: 1px solid #ddd;
            border-radius: 4px;
            font-family: monospace;
            font-size: 12px;
            box-sizing: border-box;
            resize: vertical;
          ">${token}</textarea>
          
          <div style="margin-top: 20px; display: flex; gap: 10px; justify-content: flex-end;">
            <button onclick="document.getElementById('jwt-modal-overlay').remove()" style="
              padding: 10px 20px;
              background-color: #ccc;
              border: none;
              border-radius: 4px;
              cursor: pointer;
              font-size: 14px;
            ">Cancel</button>
            
            <button onclick="saveJwtToken()" style="
              padding: 10px 20px;
              background-color: #49cc90;
              color: white;
              border: none;
              border-radius: 4px;
              cursor: pointer;
              font-size: 14px;
              font-weight: bold;
            ">Authorize</button>
          </div>
          
          <p style="color: #999; font-size: 12px; margin-top: 15px; border-top: 1px solid #eee; padding-top: 15px;">
            💡 Tip: Get a token from your Login/Auth endpoint, then paste it here. It will be automatically added to all API requests.
          </p>
        </div>
      </div>
    `;
    
    document.body.insertAdjacentHTML('beforeend', html);
    
    // Focus on textarea
    setTimeout(() => {
      const input = document.getElementById('jwt-token-input');
      if (input) input.focus();
    }, 100);
  }

  window.saveJwtToken = function() {
    const input = document.getElementById('jwt-token-input');
    const token = input.value.trim();
    
    if (!token) {
      alert('Please enter a JWT token');
      return;
    }
    
    // Remove "Bearer " prefix if present
    const cleanToken = token.replace(/^Bearer\s+/i, '');
    
    window.jwtToken = cleanToken;
    localStorage.setItem('jwt_token', cleanToken);
    
    document.getElementById('jwt-modal-overlay').remove();
    alert('✅ JWT token saved! It will be used for all API requests.');
  };

  window.showJwtModal = showJwtModal;

  // Initialize when page loads or when DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', setupJwtAuthorization);
  } else {
    setupJwtAuthorization();
  }

  // Also run on window load
  window.addEventListener('load', setupJwtAuthorization);
})();
