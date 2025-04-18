function parseFormJson(form){
    const object = {}
    const formData = new FormData(form)

    formData.forEach((value,key)=>{
        object[key]=value
    })

    return JSON.stringify(object)
}

const arr = Array.from(document.getElementsByClassName('text-to-decimal'))

arr.forEach(e=>{
    const targetId = e.dataset.target;
    const control = document.getElementById(targetId)
    control.hidden=true
    e.addEventListener('input',()=>{
        let valor = e.value.replace(/\./g,'')

        const somenteVirgula = (valor.match(/,/g) || []).length;
        if(somenteVirgula>1){
            const arrGeradoVirgulas = valor.split(',')
            valor=arrGeradoVirgulas[0]+','+arrGeradoVirgulas.slice(1).join('')
            e.value=valor
        }

        if(valor==''){
            valor='0'
            e.value='0'
        }

        control.value=valor.replace(',','.')
    })

    e.value = document.getElementById(targetId).value.replace('.',',')
})

document.querySelector('form').addEventListener('keydown', function(e) {
    if (e.key === 'Enter') {
        e.preventDefault();
    }
});