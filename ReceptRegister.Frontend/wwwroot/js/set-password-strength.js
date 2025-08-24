// Progressive enhancement: evaluate password strength client-side mirroring server PasswordStrength.Evaluate
// Score scale 0-6 based on: length>=8, length>=12, lower, upper, digit, symbol. Acceptable if score>=3.
const pwd = document.getElementById('Password');
const strength = document.getElementById('pwStrength');
if (pwd && strength) {
  const meterId = 'pw-meter';
  let meter = document.getElementById(meterId);
  if (!meter) {
    meter = document.createElement('div');
    meter.id = meterId;
    meter.setAttribute('aria-hidden','true');
    meter.className = 'pw-meter';
    for (let i=0;i<6;i++){ const bar=document.createElement('span'); bar.className='pw-meter-bar'; meter.appendChild(bar);}    
    strength.parentNode.insertBefore(meter, strength.nextSibling);
  }
  const suggList = document.createElement('ul');
  suggList.className = 'pw-suggestions';
  strength.parentNode.insertBefore(suggList, meter.nextSibling);
  function evaluate(p){
    if(!p) return {score:0,label:'Empty',suggestions:['Add a password']};
    let s=0; const suggestions=[];
    if(p.length>=8) s++; else suggestions.push('Use at least 8 characters');
    if(p.length>=12) s++; else suggestions.push('Use 12+ characters');
    if(/[a-z]/.test(p)) s++; else suggestions.push('Add a lowercase letter');
    if(/[A-Z]/.test(p)) s++; else suggestions.push('Add an uppercase letter');
    if(/\d/.test(p)) s++; else suggestions.push('Add a digit');
    if(/[^A-Za-z0-9]/.test(p)) s++; else suggestions.push('Add a symbol');
    let label = s<=2?'Weak': s<=4?'Fair': s===5?'Good':'Strong';
    return {score:s,label,suggestions};
  }
  let debounceTimer;
  function update(){
    const val = pwd.value;
    if(!val){ strength.textContent=''; meter.querySelectorAll('.pw-meter-bar').forEach(b=>b.className='pw-meter-bar'); suggList.innerHTML=''; return; }
    const r = evaluate(val);
    strength.textContent = `Strength: ${r.label}`;
    strength.className = 'form-text pw-strength-' + r.label.toLowerCase();
    const bars = meter.querySelectorAll('.pw-meter-bar');
    bars.forEach((b,i)=>{ b.className = 'pw-meter-bar' + (i<r.score? ' filled score-'+r.score:''); });
    suggList.innerHTML = '';
    r.suggestions.slice(0,3).forEach(s=>{ const li=document.createElement('li'); li.textContent=s; suggList.appendChild(li); });
  }
  pwd.addEventListener('input', ()=>{ clearTimeout(debounceTimer); debounceTimer=setTimeout(update,120); });
}
