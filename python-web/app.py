from flask import Flask, request, render_template_string
import os

app = Flask(__name__)

@app.route('/')
def index():
    return '''
    <h1>Python Web App</h1>
    <form action="/render" method="get">
        <input type="text" name="template" placeholder="Template">
        <input type="text" name="name" placeholder="Name">
        <button type="submit">Render</button>
    </form>
    '''

@app.route('/render')
def render():
    template = request.args.get('template', 'Hello {{name}}')
    name = request.args.get('name', 'World')
    # VULNERABILITY: SSTI
    return render_template_string(template, name=name)

@app.route('/health')
def health():
    return 'OK', 200

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000)
